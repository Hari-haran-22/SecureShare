using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SecureShare.API.Data;
using SecureShare.API.Services;
using SecureShare.Core.Entities;
using SecureShare.Core.Interfaces;
using Xunit;

namespace SecureShare.Tests;

public class FileFlowTests
{
    private static async Task<HttpClient> OwnerAsync(ApiFixture factory)
    {
        var client = factory.CreateClient();
        var result = await client.GetAsync("/api/session");
        Assert.True(result.IsSuccessStatusCode, await result.Content.ReadAsStringAsync());
        var response = await result.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", response.GetProperty("csrfToken").GetString());
        return client;
    }

    private static async Task<Guid> UploadAsync(HttpClient client, string? password = null, int count = 1)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("secret file content"u8.ToArray()), "file", "test.txt");
        form.Add(new StringContent("24"), "expiryHours");
        form.Add(new StringContent(count.ToString()), "maxDownloads");
        if (password != null) form.Add(new StringContent(password), "password");
        var result = await client.PostAsync("/api/files/upload", form);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        return (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task OwnerIsolationPasswordsAndRevocation()
    {
        using var factory = new ApiFixture();
        await factory.InitializeDatabaseAsync();
        using var owner = await OwnerAsync(factory);
        using var stranger = await OwnerAsync(factory);
        var id = await UploadAsync(owner, "strong-password", 2);
        Assert.Empty((await stranger.GetFromJsonAsync<JsonElement[]>("/api/files"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync("/api/files/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync("/api/files/" + id + "/logs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await stranger.PostAsJsonAsync("/api/files/" + id + "/download", new {password="wrong"})).StatusCode);
        var download = await stranger.PostAsJsonAsync("/api/files/" + id + "/download", new {password="strong-password"});
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("secret file content", await download.Content.ReadAsStringAsync());
        var logs = await owner.GetFromJsonAsync<JsonElement[]>("/api/files/" + id + "/logs");
        Assert.Single(logs!);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/files/" + id + "/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync("/api/files/" + id + "/info")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentRequestsClaimOnlyOneDownload()
    {
        using var factory = new ApiFixture();
        await factory.InitializeDatabaseAsync();
        using var owner = await OwnerAsync(factory);
        var id = await UploadAsync(owner);
        var clients = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => OwnerAsync(factory)));
        try
        {
            var responses = await Task.WhenAll(clients.Select(c => Task.Run(() =>
                c.PostAsJsonAsync("/api/files/" + id + "/download", new {password=(string?)null}))));
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, (await db.FileRecords.SingleAsync(f => f.Id == id)).DownloadCount);
            Assert.Equal(1, await db.AccessLogs.CountAsync());
        }
        finally { foreach (var client in clients) client.Dispose(); }
    }

    [Fact]
    public async Task MissingFileDoesNotConsumeAllowanceAndGetDoesNotDownload()
    {
        using var factory = new ApiFixture();
        await factory.InitializeDatabaseAsync();
        using var owner = await OwnerAsync(factory);
        var id = await UploadAsync(owner);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var record = await db.FileRecords.SingleAsync(f => f.Id == id);
            Assert.True(record.KeyProtected);
            Assert.NotEqual(32, record.EncryptionKey.Length);
            Assert.Equal(2, record.EncryptionVersion);
            Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/files/" + id)).StatusCode);
            await scope.ServiceProvider.GetRequiredService<IFileStorageService>().DeleteFileAsync(record.StoredFilename);
        }
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await owner.PostAsJsonAsync("/api/files/" + id + "/download", new {password=(string?)null})).StatusCode);
        using var check = factory.Services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, (await database.FileRecords.SingleAsync(f => f.Id == id)).DownloadCount);
        Assert.Equal(0, await database.AccessLogs.CountAsync());
    }

    [Fact]
    public async Task CleanupDeletesExpiredDataAndKeys()
    {
        using var factory = new ApiFixture();
        await factory.InitializeDatabaseAsync();
        using var owner = await OwnerAsync(factory);
        var id = await UploadAsync(owner);
        string name;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var record = await db.FileRecords.SingleAsync(f => f.Id == id);
            name = record.StoredFilename;
            record.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        var cleaner = ActivatorUtilities.CreateInstance<FileCleanupService>(factory.Services);
        await cleaner.CleanAsync(CancellationToken.None);
        cleaner.Dispose();
        using var check = factory.Services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var retired = await database.FileRecords.SingleAsync(f => f.Id == id);
        Assert.False(retired.IsActive);
        Assert.Empty(retired.EncryptionKey);
        Assert.Empty(retired.IV);
        Assert.Equal("", retired.StoredFilename);
        Assert.False(File.Exists(Path.Combine(factory.Root, "uploads", name)));
    }

    [Fact]
    public async Task EnforcesCsrfAndServerSideExpiryValidation()
    {
        using var factory = new ApiFixture();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(new byte[] {1}), "file", "test.txt");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/files/upload", form)).StatusCode);
        await client.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/files/upload", form)).StatusCode);
        using var owner = await OwnerAsync(factory);
        using var invalid = new MultipartFormDataContent();
        invalid.Add(new ByteArrayContent(new byte[] {1}), "file", "test.txt");
        invalid.Add(new StringContent("-1"), "expiryHours");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/files/upload", invalid)).StatusCode);
    }

    [Fact]
    public async Task RecoveryCodeRestoresOnlyTheMatchingDashboard()
    {
        using var factory = new ApiFixture();
        await factory.InitializeDatabaseAsync();
        using var owner = await OwnerAsync(factory);
        var session = await owner.GetFromJsonAsync<JsonElement>("/api/session");
        var id = await UploadAsync(owner);
        using var other = await OwnerAsync(factory);
        var response = await other.PostAsJsonAsync("/api/session/restore", new {code=session.GetProperty("recoveryCode").GetString()});
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var restored = await other.GetFromJsonAsync<JsonElement[]>("/api/files");
        Assert.Equal(id, Assert.Single(restored!).GetProperty("file").GetProperty("id").GetGuid());
    }
}
