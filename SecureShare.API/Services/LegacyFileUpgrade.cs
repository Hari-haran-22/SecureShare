using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SecureShare.API.Data;
using SecureShare.Core.Interfaces;

namespace SecureShare.API.Services;

public class LegacyFileUpgrade(ApplicationDbContext db, IFileEncryptionService encryption,
    IFileStorageService storage, FileKeyProtector keys, ILogger<LegacyFileUpgrade> logger)
{
    public async Task RunAsync()
    {
        var old = await db.FileRecords.AsNoTracking().Where(f => f.EncryptionVersion == 0 && f.IsActive).ToListAsync();
        foreach (var record in old)
        {
            string? newName = null;
            byte[]? oldKey = null;
            byte[]? newKey = null;
            try
            {
                oldKey = keys.Unprotect(record);
                await using var plain = TemporaryFile.Create();
                await using (var source = await storage.GetFileStreamAsync(record.StoredFilename))
                    await encryption.DecryptAsync(source, plain, oldKey, record.IV, 0);
                var size = plain.Length;
                plain.Position = 0;
                await using var output = TemporaryFile.Create();
                var encrypted = await encryption.EncryptAsync(plain, output);
                newKey = encrypted.Key;
                var protectedKey = keys.Protect(newKey);
                output.Position = 0;
                newName = await storage.SaveFileAsync(output, record.OriginalFilename);
                var changed = await db.FileRecords.Where(f => f.Id == record.Id &&
                    f.EncryptionVersion == 0 && f.StoredFilename == record.StoredFilename && f.IsActive)
                    .ExecuteUpdateAsync(s => s.SetProperty(f => f.StoredFilename, newName)
                        .SetProperty(f => f.EncryptionKey, protectedKey)
                        .SetProperty(f => f.IV, encrypted.IV)
                        .SetProperty(f => f.SizeBytes, size)
                        .SetProperty(f => f.EncryptionVersion, 2)
                        .SetProperty(f => f.KeyProtected, true));
                // An update can commit even if its acknowledgement is lost. A retry
                // then changes zero rows; reconcile before deleting either copy.
                if (changed == 1 || await db.FileRecords.AsNoTracking().AnyAsync(f => f.StoredFilename == newName))
                    await storage.DeleteFileAsync(record.StoredFilename);
                else await storage.DeleteFileAsync(newName);
            }
            catch (Exception ex) when (ex is IOException or CryptographicException)
            {
                logger.LogError("Legacy file {Id} could not be upgraded: {Type}", record.Id, ex.GetType().Name);
                var installed = newName != null && await db.FileRecords.AsNoTracking().AnyAsync(f => f.StoredFilename == newName);
                if (!installed)
                {
                    await db.FileRecords.Where(f => f.Id == record.Id && f.EncryptionVersion == 0)
                        .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsActive, false));
                    if (newName != null) await storage.DeleteFileAsync(newName);
                }
            }
            finally
            {
                if (oldKey != null) CryptographicOperations.ZeroMemory(oldKey);
                if (newKey != null) CryptographicOperations.ZeroMemory(newKey);
            }
        }
    }
}
