using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SecureShare.API.Services;

public class ScannerHealthCheck(IConfiguration config, IWebHostEnvironment environment) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!config.GetValue("Scanner:Enabled", !environment.IsDevelopment()))
            return HealthCheckResult.Healthy("Scanning disabled for development");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var client = new TcpClient();
            await client.ConnectAsync(config["Scanner:Host"] ?? "clamav", config.GetValue("Scanner:Port", 3310), timeout.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync("zPING\0"u8.ToArray(), timeout.Token);
            var buffer = new byte[16];
            var count = await stream.ReadAsync(buffer, timeout.Token);
            return System.Text.Encoding.UTF8.GetString(buffer, 0, count).StartsWith("PONG", StringComparison.Ordinal)
                ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Scanner not ready");
        }
        catch { return HealthCheckResult.Unhealthy("Scanner unavailable"); }
    }
}
