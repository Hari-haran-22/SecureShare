using SecureShare.Core.Interfaces;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SecureShare.API.Services
{
    public class FileEncryptionService : IFileEncryptionService
    {
        public async Task<(byte[] Key, byte[] IV)> EncryptAsync(Stream inputStream, Stream outputStream)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256; // Strongest standard AES
            aes.GenerateKey();
            aes.GenerateIV();

            var key = aes.Key;
            var iv = aes.IV;

            // Create the encryptor
            using var encryptor = aes.CreateEncryptor(key, iv);
            using var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write, leaveOpen: true);

            // Copy the input file through the encryption stream
            await inputStream.CopyToAsync(cryptoStream);

            // Return the secrets so we can save them (securely) later
            return (key, iv);
        }

        public async Task DecryptAsync(Stream inputStream, Stream outputStream, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);

            await cryptoStream.CopyToAsync(outputStream);
        }
    }
}