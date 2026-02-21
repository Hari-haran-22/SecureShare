using SecureShare.Core.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SecureShare.API.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly string _storagePath;

        public LocalFileStorageService(IWebHostEnvironment env)
        {
            // Saves files in a folder named "SecureUploads" in the project root
            _storagePath = Path.Combine(env.ContentRootPath, "SecureUploads");

            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
            }
        }

        public async Task<string> SaveFileAsync(Stream fileStream, string fileName)
        {
            // We use a GUID name to prevent overwriting existing files
            var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
            var fullPath = Path.Combine(_storagePath, uniqueFileName);

            using var fileOutput = new FileStream(fullPath, FileMode.Create);
            await fileStream.CopyToAsync(fileOutput);

            return uniqueFileName;
        }

        public Task<Stream> GetFileStreamAsync(string storedFileName)
        {
            var fullPath = Path.Combine(_storagePath, storedFileName);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("File not found on disk.");
            }

            // Return read-only stream
            return Task.FromResult<Stream>(new FileStream(fullPath, FileMode.Open, FileAccess.Read));
        }

        public Task DeleteFileAsync(string storedFileName)
        {
            var fullPath = Path.Combine(_storagePath, storedFileName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            return Task.CompletedTask;
        }
    }
}