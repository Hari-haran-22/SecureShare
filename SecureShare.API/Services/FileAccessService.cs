using Microsoft.EntityFrameworkCore;
using SecureShare.API.Data;
using SecureShare.Core.Entities;

namespace SecureShare.API.Services;

public class FileAccessService(ApplicationDbContext db)
{
    // One conditional SQL update claims an allowance. Log and count commit together.
    // EF's retry strategy retries the entire transaction, never just the increment.
    public async Task<bool> ClaimAsync(Guid id, string ip, string userAgent, CancellationToken ct)
    {
        var operationId = Guid.NewGuid();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (await db.AccessLogs.AsNoTracking().AnyAsync(l => l.OperationId == operationId, ct)) return true;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var now = DateTime.UtcNow;
            var changed = await db.FileRecords.Where(f => f.Id == id && f.IsActive &&
                (!f.ExpiresAt.HasValue || f.ExpiresAt > now) &&
                (!f.MaxDownloads.HasValue || f.DownloadCount < f.MaxDownloads))
                .ExecuteUpdateAsync(set => set
                    .SetProperty(f => f.DownloadCount, f => f.DownloadCount + 1)
                    .SetProperty(f => f.IsActive, f => !f.MaxDownloads.HasValue || f.DownloadCount + 1 < f.MaxDownloads), ct);
            if (changed == 0) return false;
            // Raw insert avoids retaining tracked entities across retries.
            await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO AccessLogs (FileRecordId, AccessedAt, IpAddress, UserAgent, OperationId)
                VALUES ({id}, {now}, {ip}, {userAgent}, {operationId})", ct);
            await transaction.CommitAsync(ct);
            return true;
        });
    }
}
