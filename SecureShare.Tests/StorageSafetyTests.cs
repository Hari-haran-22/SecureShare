using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SecureShare.API.Data;
using SecureShare.API.Services;
using SecureShare.Core.Entities;
using SecureShare.Core.Interfaces;
using Xunit;

namespace SecureShare.Tests;

public class StorageSafetyTests
{
    [Fact]
    public async Task FailedDatabaseWriteRemovesTheUncommittedFile()
    {
        using var root = new ApiFixture();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(s =>
            s.AddDbContext<ApplicationDbContext>(o => o.AddInterceptors(new FailingWriteInterceptor()))));
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
        using var owner = await ClientAsync(factory);
        Assert.Equal(HttpStatusCode.InternalServerError, (await UploadAsync(owner)).StatusCode);
        Assert.Empty(Directory.GetFiles(Path.Combine(root.Root, "uploads")));
        using var check = factory.Services.CreateScope();
        Assert.Empty(await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().FileRecords.ToListAsync());
    }

    private static async Task<HttpClient> ClientAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var session = await client.GetFromJsonAsync<JsonElement>("/api/session");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.GetProperty("csrfToken").GetString());
        return client;
    }
    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("secret file content"u8.ToArray()), "file", "file.txt");
        return await client.PostAsync("/api/files/upload", form);
    }

    [Fact]
    public async Task GlobalQuotaRejectsAnotherOwnerWithoutLeavingOrphans()
    {
        using var root = new ApiFixture();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Storage:MaxFileBytes"] = "32", ["Storage:OwnerQuotaBytes"] = "32", ["Storage:TotalQuotaBytes"] = "32"
            })));
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
        using var owner = await ClientAsync(factory);
        using var other = await ClientAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(owner)).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await UploadAsync(other)).StatusCode);
        using var check = factory.Services.CreateScope();
        Assert.Equal(1, await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().FileRecords.CountAsync());
        Assert.Single(Directory.GetFiles(Path.Combine(root.Root, "uploads")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MalwareAndScannerFailuresRejectUploads(bool unavailable)
    {
        using var root = new ApiFixture();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(s => {
            s.RemoveAll<IFileScanner>();
            s.AddSingleton<IFileScanner>(new RejectingScanner(unavailable));
        }));
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
        using var owner = await ClientAsync(factory);
        Assert.Equal(unavailable ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.UnprocessableEntity,
            (await UploadAsync(owner)).StatusCode);
        using var check = factory.Services.CreateScope();
        Assert.Equal(0, await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().FileRecords.CountAsync());
        Assert.Empty(Directory.GetFiles(Path.Combine(root.Root, "uploads")));
    }

    [Fact]
    public async Task TamperedCiphertextIsNeverDeliveredOrCounted()
    {
        using var factory = new ApiFixture();
        await factory.InitializeDatabaseAsync();
        using var owner = await ClientAsync(factory);
        var uploaded = await UploadAsync(owner);
        var id = (await uploaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var record = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().FileRecords.SingleAsync(f => f.Id == id);
            var path = Path.Combine(factory.Root, "uploads", record.StoredFilename);
            var bytes = await File.ReadAllBytesAsync(path);
            bytes[10] ^= 1;
            await File.WriteAllBytesAsync(path, bytes);
        }
        var result = await owner.PostAsJsonAsync("/api/files/" + id + "/download", new {password=(string?)null});
        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, (await db.FileRecords.SingleAsync(f => f.Id == id)).DownloadCount);
        Assert.Empty(await db.AccessLogs.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyFilesUpgradeWithoutChangingTheirContentOrLink(bool failOldFileDeletion)
    {
        using var factory = new ApiFixture();
        await factory.InitializeDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        using var aes = Aes.Create();
        var data = "legacy secret"u8.ToArray();
        using var ciphertext = new MemoryStream();
        using (var crypto = new CryptoStream(ciphertext, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen:true))
            await crypto.WriteAsync(data);
        ciphertext.Position = 0;
        var oldName = await storage.SaveFileAsync(ciphertext, "old.txt");
        var record = new FileRecord {
            Id=Guid.NewGuid(), OriginalFilename="old.txt", StoredFilename=oldName,
            IsActive=true, UploadedAt=DateTime.UtcNow, ExpiresAt=DateTime.UtcNow.AddHours(24),
            EncryptionKey=aes.Key, IV=aes.IV, EncryptionVersion=0
        };
        db.Add(record); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var upgradeStorage = failOldFileDeletion ? new DeleteFailureStorage(storage, oldName) : storage;
        var upgrade = new LegacyFileUpgrade(db, scope.ServiceProvider.GetRequiredService<IFileEncryptionService>(),
            upgradeStorage, scope.ServiceProvider.GetRequiredService<FileKeyProtector>(),
            scope.ServiceProvider.GetRequiredService<ILogger<LegacyFileUpgrade>>());
        await upgrade.RunAsync();
        var upgraded = await db.FileRecords.SingleAsync(f => f.Id == record.Id);
        Assert.Equal(2, upgraded.EncryptionVersion); Assert.True(upgraded.KeyProtected);
        Assert.Equal(failOldFileDeletion, File.Exists(Path.Combine(factory.Root, "uploads", oldName)));
        using var encrypted = await storage.GetFileStreamAsync(upgraded.StoredFilename);
        using var plain = new MemoryStream();
        var key = scope.ServiceProvider.GetRequiredService<FileKeyProtector>().Unprotect(upgraded);
        await scope.ServiceProvider.GetRequiredService<IFileEncryptionService>().DecryptAsync(encrypted, plain, key, upgraded.IV);
        Assert.Equal(data, plain.ToArray());
    }

    private class RejectingScanner(bool unavailable) : IFileScanner
    {
        public Task<bool> IsCleanAsync(Stream stream, CancellationToken ct) =>
            unavailable ? throw new IOException("scanner unavailable") : Task.FromResult(false);
    }

    private class DeleteFailureStorage(IFileStorageService inner, string oldName) : IFileStorageService
    {
        public Task<string> SaveFileAsync(Stream stream, string name) => inner.SaveFileAsync(stream, name);
        public Task<Stream> GetFileStreamAsync(string name) => inner.GetFileStreamAsync(name);
        public Task DeleteFileAsync(string name) => name == oldName
            ? throw new IOException("Old file cleanup failed") : inner.DeleteFileAsync(name);
    }

    private class FailingWriteInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Simulated database write failure");
    }
}
