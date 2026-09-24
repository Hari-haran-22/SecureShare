using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prometheus;
using SecureShare.API.Data;
using SecureShare.Core.Interfaces;

namespace SecureShare.API.Services;

public class FileCleanupService(IServiceScopeFactory scopes, IOptions<StorageOptions> options,
    ILogger<FileCleanupService> logger) : BackgroundService
{
    private static readonly Gauge StoredBytes = Metrics.CreateGauge("secureshare_stored_bytes", "Retained original file bytes");
    private static readonly Gauge ActiveFiles = Metrics.CreateGauge("secureshare_active_files", "Available share links");
    private static readonly Gauge LastSuccess = Metrics.CreateGauge("secureshare_cleanup_last_success_seconds", "Last successful cleanup Unix timestamp");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.CleanupSeconds));
        do
        {
            try { await CleanAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "File cleanup failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task CleanAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        var now = DateTime.UtcNow;
        await db.FileRecords.Where(f => f.IsActive &&
            ((f.ExpiresAt.HasValue && f.ExpiresAt <= now) ||
             (f.MaxDownloads.HasValue && f.DownloadCount >= f.MaxDownloads)))
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsActive, false), ct);
        var expired = await db.FileRecords.AsNoTracking().Where(f => !f.IsActive && f.StoredFilename != "")
            .Select(f => new { f.Id, f.StoredFilename }).Take(500).ToListAsync(ct);
        foreach (var file in expired)
        {
            await storage.DeleteFileAsync(file.StoredFilename);
            await db.FileRecords.Where(f => f.Id == file.Id && !f.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.StoredFilename, "")
                    .SetProperty(f => f.EncryptionKey, Array.Empty<byte>())
                    .SetProperty(f => f.IV, Array.Empty<byte>())
                    .SetProperty(f => f.PasswordHash, (string?)null), ct);
        }
        var cutoff = now.AddDays(-options.Value.AuditRetentionDays);
        await db.AccessLogs.Where(l => l.AccessedAt < cutoff).ExecuteDeleteAsync(ct);
        await db.FileRecords.Where(f => !f.IsActive && f.StoredFilename == "" &&
            (f.ExpiresAt ?? f.UploadedAt) < cutoff).ExecuteDeleteAsync(ct);
        // Reconcile files left by a process crash or an unavailable database.
        if (storage is LocalFileStorageService local)
        {
            foreach (var path in Directory.EnumerateFiles(local.RootPath))
            {
                if (File.GetLastWriteTimeUtc(path) > now.AddDays(-1)) continue;
                var name = Path.GetFileName(path);
                if (!await db.FileRecords.AnyAsync(f => f.StoredFilename == name, ct))
                    await storage.DeleteFileAsync(name);
            }
        }
        StoredBytes.Set(await db.FileRecords.Where(f => f.StoredFilename != "").SumAsync(f => (long?)f.SizeBytes, ct) ?? 0);
        ActiveFiles.Set(await db.FileRecords.CountAsync(f => f.IsActive, ct));
        LastSuccess.Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }
}
