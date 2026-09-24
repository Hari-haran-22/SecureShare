using System.Buffers.Binary;
using System.Security.Cryptography;
using SecureShare.Core.Interfaces;

namespace SecureShare.API.Services;

// Each 64 KiB AES-GCM frame authenticates its sequence and length.
// A final authenticated empty frame prevents truncation.
public class FileEncryptionService : IFileEncryptionService
{
    private const int ChunkSize = 65536;
    public async Task<(byte[] Key, byte[] IV)> EncryptAsync(Stream input, Stream output)
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var iv = RandomNumberGenerator.GetBytes(8);
        using var aes = new AesGcm(key, 16);
        var plain = new byte[ChunkSize];
        var cipher = new byte[ChunkSize];
        var tag = new byte[16];
        uint sequence = 0;
        try
        {
            while (true)
            {
                var count = await input.ReadAtLeastAsync(plain, ChunkSize, throwOnEndOfStream: false);
                var header = Header(sequence, count);
                aes.Encrypt(Nonce(iv, sequence++), plain.AsSpan(0, count), cipher.AsSpan(0, count), tag, header);
                await output.WriteAsync(header);
                await output.WriteAsync(cipher.AsMemory(0, count));
                await output.WriteAsync(tag);
                if (count == 0) break;
            }
            return (key, iv);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public async Task DecryptAsync(Stream input, Stream output, byte[] key, byte[] iv, int version = 2)
    {
        if (version == 0)
        {
            using var legacy = Aes.Create();
            using var decryptor = legacy.CreateDecryptor(key, iv);
            using var crypto = new CryptoStream(input, decryptor, CryptoStreamMode.Read, leaveOpen: true);
            await crypto.CopyToAsync(output);
            return;
        }
        if (version != 2 || iv.Length != 8) throw new CryptographicException("Unsupported file format.");
        using var aes = new AesGcm(key, 16);
        var header = new byte[8];
        var cipher = new byte[ChunkSize];
        var plain = new byte[ChunkSize];
        var tag = new byte[16];
        uint sequence = 0;
        try
        {
            while (true)
            {
                await input.ReadExactlyAsync(header);
                var count = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4));
                if (BinaryPrimitives.ReadUInt32BigEndian(header) != sequence || count < 0 || count > ChunkSize)
                    throw new CryptographicException("Invalid file frame.");
                await input.ReadExactlyAsync(cipher.AsMemory(0, count));
                await input.ReadExactlyAsync(tag);
                aes.Decrypt(Nonce(iv, sequence++), cipher.AsSpan(0, count), tag, plain.AsSpan(0, count), header);
                if (count == 0)
                {
                    if (input.ReadByte() != -1) throw new CryptographicException("Trailing file data.");
                    break;
                }
                await output.WriteAsync(plain.AsMemory(0, count));
            }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private static byte[] Header(uint sequence, int length)
    {
        var header = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(header, sequence);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), length);
        return header;
    }
    private static byte[] Nonce(byte[] prefix, uint sequence)
    {
        var nonce = new byte[12];
        prefix.CopyTo(nonce, 0);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8), sequence);
        return nonce;
    }
}
