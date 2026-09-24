using System.Net.Sockets;
using System.Buffers.Binary;

namespace SecureShare.API.Services;

// ClamAV INSTREAM. Scanner errors fail uploads closed.
public class FileScanner(IConfiguration config, IWebHostEnvironment environment) : IFileScanner
{
    public async Task<bool> IsCleanAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (!config.GetValue("Scanner:Enabled", !environment.IsDevelopment())) return true;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        using var client = new TcpClient();
        await client.ConnectAsync(config["Scanner:Host"] ?? "clamav", config.GetValue("Scanner:Port", 3310), timeout.Token);
        await using var socket = client.GetStream();
        await socket.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);
        var buffer = new byte[65536];
        var length = new byte[4];
        int count;
        while ((count = await stream.ReadAsync(buffer, timeout.Token)) > 0)
        {
            BinaryPrimitives.WriteInt32BigEndian(length, count);
            await socket.WriteAsync(length, timeout.Token);
            await socket.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
        }
        await socket.WriteAsync(new byte[4], timeout.Token);
        var response = new List<byte>();
        var single = new byte[1];
        while (response.Count < 4096 && await socket.ReadAsync(single, timeout.Token) > 0)
        {
            if (single[0] == 0) break;
            response.Add(single[0]);
        }
        return System.Text.Encoding.UTF8.GetString(response.ToArray()).EndsWith(": OK", StringComparison.Ordinal);
    }
}
