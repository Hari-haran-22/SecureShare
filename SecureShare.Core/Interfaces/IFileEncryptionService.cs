using System.IO;
using System.Threading.Tasks;

namespace SecureShare.Core.Interfaces
{
    public interface IFileEncryptionService
    {
        // Encrypts a file stream and returns the encrypted stream + the key used
        Task<(byte[] Key, byte[] IV)> EncryptAsync(Stream inputStream, Stream outputStream);

        // Decrypts a file stream using the provided key and IV
        Task DecryptAsync(Stream inputStream, Stream outputStream, byte[] key, byte[] iv);
    }
}
