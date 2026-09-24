using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SecureShare.API.Services;

public class StorageHealthCheck(LocalFileStorageService storage) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = Path.Combine(storage.RootPath, $".health-{Guid.NewGuid():N}");
            using (new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch { return Task.FromResult(HealthCheckResult.Unhealthy("Storage unavailable")); }
    }
}
