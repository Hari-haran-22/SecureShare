using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureShare.API.Data;
using SecureShare.API.Services;
using SecureShare.Core.Entities;
using SecureShare.Core.Interfaces;

namespace SecureShare.API.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController(ApplicationDbContext db, IFileEncryptionService encryption,
    IFileStorageService storage, FileKeyProtector keys, IFileScanner scanner,
    FileAccessService access, IPasswordHasher<FileRecord> passwords, IOptions<StorageOptions> options,
    ILogger<FilesController> logger) : ControllerBase
{
    private string OwnerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private readonly StorageOptions limits = options.Value;
    private string ShareUrl(Guid id) => $"/download.html?id={id}";

    [Authorize]
    [EnableRateLimiting("upload")]
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] int expiryHours = 24,
        [FromForm] int maxDownloads = 1, [FromForm] string? password = null)
    {
        if (file == null || file.Length == 0 || file.Length > limits.MaxFileBytes)
            return BadRequest(new { detail = $"Choose a non-empty file up to {limits.MaxFileBytes / 1048576} MB." });
        if (expiryHours < 1 || expiryHours > limits.MaxExpiryHours || maxDownloads < 1 || maxDownloads > limits.MaxDownloads)
            return BadRequest(new { detail = "Expiry or download limit is outside the allowed range." });
        if (password != null && (password.Length < 8 || password.Length > 128))
            return BadRequest(new { detail = "Passwords must contain 8–128 characters." });
        var filename = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(filename) || filename.Length > 255 || filename.Any(char.IsControl))
            return BadRequest(new { detail = "Invalid filename." });
        if (!await db.Database.CanConnectAsync(HttpContext.RequestAborted))
            return StatusCode(503, new { detail = "Storage metadata is unavailable. Please try again later." });

        using var input = file.OpenReadStream();
        try
        {
            if (!await scanner.IsCleanAsync(input, HttpContext.RequestAborted))
                return UnprocessableEntity(new { detail = "This file did not pass the malware scan." });
        }
        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or OperationCanceledException)
        {
            logger.LogWarning("Upload scanner unavailable: {Type}", ex.GetType().Name);
            return StatusCode(503, new { detail = "File scanning is unavailable. Please try again later." });
        }
        input.Position = 0;
        HttpContext.RequestAborted.ThrowIfCancellationRequested();
        await using var encrypted = TemporaryFile.Create();
        var (key, iv) = await encryption.EncryptAsync(input, encrypted);
        encrypted.Position = 0;
        var record = new FileRecord {
            Id = Guid.NewGuid(), OwnerId = OwnerId, OriginalFilename = filename,
            UploadedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(expiryHours),
            MaxDownloads = maxDownloads, SizeBytes = file.Length, IsActive = true,
            EncryptionKey = keys.Protect(key), IV = iv, EncryptionVersion = 2, KeyProtected = true
        };
        CryptographicOperations.ZeroMemory(key);
        if (password != null) record.PasswordHash = passwords.HashPassword(record, password);
        var storedName = await storage.SaveFileAsync(encrypted, filename);
        record.StoredFilename = storedName;
        var commitAttempted = false;
        try
        {
            HttpContext.RequestAborted.ThrowIfCancellationRequested();
            // Serialize quota checks across replicas with a transaction-scoped SQL lock.
            var accepted = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                if (db.Database.IsSqlServer()) await db.Database.ExecuteSqlRawAsync(@"
                    DECLARE @result int;
                    EXEC @result = sp_getapplock @Resource='SecureShare.UploadQuota', @LockMode='Exclusive',
                        @LockOwner='Transaction', @LockTimeout=10000;
                    IF @result < 0 THROW 51000, 'Quota lock unavailable', 1;");
                if (await db.FileRecords.AsNoTracking().AnyAsync(f => f.Id == record.Id))
                    return true; // A commit acknowledgement may have been lost during a retry.
                var retained = db.FileRecords.Where(f => f.StoredFilename != "");
                var ownerFiles = retained.Where(f => f.OwnerId == OwnerId);
                var ownerBytes = await ownerFiles.SumAsync(f => (long?)f.SizeBytes) ?? 0;
                var totalBytes = await retained.SumAsync(f => (long?)f.SizeBytes) ?? 0;
                if (await ownerFiles.CountAsync() >= limits.MaxFilesPerOwner ||
                    await retained.CountAsync() >= limits.MaxTotalFiles ||
                    ownerBytes + record.SizeBytes > limits.OwnerQuotaBytes ||
                    totalBytes + record.SizeBytes > limits.TotalQuotaBytes) return false;
                db.ChangeTracker.Clear();
                db.FileRecords.Add(record);
                await db.SaveChangesAsync();
                commitAttempted = true;
                await transaction.CommitAsync();
                return true;
            });
            if (!accepted)
            {
                await storage.DeleteFileAsync(storedName);
                return StatusCode(413, new { detail = "Storage quota reached. Delete uploads or try again later." });
            }
        }
        catch
        {
            // A lost commit acknowledgement must not cause deletion of a committed file.
            try
            {
                db.ChangeTracker.Clear();
                if (!commitAttempted || !await db.FileRecords.AsNoTracking().AnyAsync(f => f.Id == record.Id))
                    await storage.DeleteFileAsync(storedName);
            }
            catch { logger.LogWarning("Upload reconciliation deferred to orphan cleanup"); }
            throw;
        }
        return Ok(new { id = record.Id, url = ShareUrl(record.Id), expiresAt = record.ExpiresAt });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var now = DateTime.UtcNow;
        var files = await db.FileRecords.AsNoTracking().Where(f => f.OwnerId == OwnerId)
            .OrderByDescending(f => f.UploadedAt).Select(f => new {
                f.Id, f.OriginalFilename, f.SizeBytes, f.UploadedAt, f.ExpiresAt,
                f.DownloadCount, f.MaxDownloads,
                isActive = f.IsActive && (!f.ExpiresAt.HasValue || f.ExpiresAt > now),
                passwordProtected = f.PasswordHash != null
            }).Take(200).ToListAsync();
        return Ok(files.Select(f => new { file = f, url = ShareUrl(f.Id) }));
    }

    [Authorize]
    [HttpGet("{id:guid}/logs")]
    public async Task<IActionResult> Logs(Guid id)
    {
        if (!await db.FileRecords.AnyAsync(f => f.Id == id && f.OwnerId == OwnerId)) return NotFound();
        return Ok(await db.AccessLogs.AsNoTracking().Where(l => l.FileRecordId == id)
            .OrderByDescending(l => l.AccessedAt).Select(l => new { l.AccessedAt, l.IpAddress, l.UserAgent })
            .Take(100).ToListAsync());
    }

    [Authorize]
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var changed = await db.FileRecords.Where(f => f.Id == id && f.OwnerId == OwnerId)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsActive, false));
        return changed == 0 ? NotFound() : Ok(new { detail = "Link revoked. Stored data will be removed by cleanup." });
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var record = await db.FileRecords.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == OwnerId);
        if (record == null) return NotFound();
        // Revocation commits first so racing downloads cannot claim an allowance.
        await db.FileRecords.Where(f => f.Id == id).ExecuteUpdateAsync(s => s.SetProperty(f => f.IsActive, false));
        if (record.StoredFilename != "") await storage.DeleteFileAsync(record.StoredFilename);
        await db.FileRecords.Where(f => f.Id == id && f.OwnerId == OwnerId).ExecuteDeleteAsync();
        return NoContent();
    }

    // Older API links remain usable, but GET never consumes a download.
    [HttpGet("{id:guid}")]
    public IActionResult Landing(Guid id) => Redirect(ShareUrl(id));

    [HttpGet("{id:guid}/info")]
    public async Task<IActionResult> Info(Guid id)
    {
        var now = DateTime.UtcNow;
        var record = await db.FileRecords.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id && f.IsActive &&
            (!f.ExpiresAt.HasValue || f.ExpiresAt > now) &&
            (!f.MaxDownloads.HasValue || f.DownloadCount < f.MaxDownloads));
        return record == null ? NotFound(new { detail = "This link is unavailable or has expired." }) :
            Ok(new { record.OriginalFilename, record.SizeBytes, record.ExpiresAt, passwordProtected = record.PasswordHash != null });
    }

    [EnableRateLimiting("download")]
    [HttpPost("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, [FromBody] DownloadRequest request)
    {
        var now = DateTime.UtcNow;
        var record = await db.FileRecords.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id && f.IsActive &&
            (!f.ExpiresAt.HasValue || f.ExpiresAt > now) &&
            (!f.MaxDownloads.HasValue || f.DownloadCount < f.MaxDownloads));
        if (record == null) return NotFound(new { detail = "This link is unavailable or has expired." });
        if (request.Password?.Length > 128) return BadRequest(new { detail = "Invalid password." });
        if (record.PasswordHash != null && passwords.VerifyHashedPassword(record, record.PasswordHash,
                request.Password ?? "") == PasswordVerificationResult.Failed)
            return StatusCode(403, new { detail = "Incorrect file password." });

        var decrypted = TemporaryFile.Create();
        try
        {
            await using (var encrypted = await storage.GetFileStreamAsync(record.StoredFilename))
            {
                var key = keys.Unprotect(record);
                try { await encryption.DecryptAsync(encrypted, decrypted, key, record.IV, record.EncryptionVersion); }
                finally { CryptographicOperations.ZeroMemory(key); }
            }
            // Corrupt or missing files never consume an allowance.
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var agent = Request.Headers.UserAgent.ToString();
            if (agent.Length > 512) agent = agent[..512];
            if (!await access.ClaimAsync(id, ip, agent, HttpContext.RequestAborted))
            {
                await decrypted.DisposeAsync();
                return NotFound(new { detail = "This link is unavailable or has expired." });
            }
            decrypted.Position = 0;
            // MVC owns and disposes this stream when the transfer finishes or disconnects.
            return File(decrypted, "application/octet-stream", record.OriginalFilename, enableRangeProcessing: false);
        }
        catch (Exception ex) when (ex is IOException or CryptographicException)
        {
            await decrypted.DisposeAsync();
            logger.LogError("Stored file {Id} failed validation: {Type}", id, ex.GetType().Name);
            return StatusCode(503, new { detail = "The stored file is unavailable. No download allowance was used." });
        }
        catch { await decrypted.DisposeAsync(); throw; }
    }
    public record DownloadRequest(string? Password);
}
