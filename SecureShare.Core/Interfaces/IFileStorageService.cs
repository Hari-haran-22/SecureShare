using System.IO;
using System.Threading.Tasks;

namespace SecureShare.Core.Interfaces
{
    public interface IFileStorageService
    {
        // Saves the encrypted file to storage and returns the stored filename
        Task<string> SaveFileAsync(Stream fileStream, string fileName);

        // Retrieves the file stream
        Task<Stream> GetFileStreamAsync(string storedFileName);

        // Deletes the file physically
        Task DeleteFileAsync(string storedFileName);
    }
}