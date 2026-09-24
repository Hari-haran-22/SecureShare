using System.Security.Cryptography;
using SecureShare.API.Services;
using Xunit;

namespace SecureShare.Tests;

public class EncryptionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(65536)]
    [InlineData(200000)]
    public async Task RoundTripAcrossFrameBoundaries(int size)
    {
        var data = RandomNumberGenerator.GetBytes(size);
        var service = new FileEncryptionService();
        using var input = new MemoryStream(data);
        using var encrypted = new MemoryStream();
        var (key, iv) = await service.EncryptAsync(input, encrypted);
        Assert.NotEqual(data, encrypted.ToArray());
        encrypted.Position = 0;
        using var output = new MemoryStream();
        await service.DecryptAsync(encrypted, output, key, iv);
        Assert.Equal(data, output.ToArray());
    }

    [Theory]
    [InlineData("ciphertext")]
    [InlineData("header")]
    [InlineData("tag")]
    [InlineData("truncate")]
    [InlineData("reorder")]
    [InlineData("append")]
    [InlineData("wrong-key")]
    public async Task RejectsTamperedFiles(string attack)
    {
        var service = new FileEncryptionService();
        using var encrypted = new MemoryStream();
        var (key, iv) = await service.EncryptAsync(new MemoryStream(RandomNumberGenerator.GetBytes(140000)), encrypted);
        var data = encrypted.ToArray();
        switch (attack)
        {
            case "ciphertext": data[20] ^= 1; break;
            case "header": data[7] ^= 1; break;
            case "tag": data[65544] ^= 1; break;
            case "truncate": data = data[..^24]; break;
            case "append": data = data.Concat(new byte[] { 1 }).ToArray(); break;
            case "wrong-key": key[0] ^= 1; break;
            case "reorder":
                var frame = data[..65560].ToArray();
                data.AsSpan(65560, 65560).CopyTo(data);
                frame.CopyTo(data, 65560);
                break;
        }
        using var output = new MemoryStream();
        await Assert.ThrowsAnyAsync<Exception>(() => service.DecryptAsync(new MemoryStream(data), output, key, iv));
    }
}
