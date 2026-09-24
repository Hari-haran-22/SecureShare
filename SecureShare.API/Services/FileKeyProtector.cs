using Microsoft.AspNetCore.DataProtection;
using SecureShare.Core.Entities;

namespace SecureShare.API.Services;

public class FileKeyProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("SecureShare.FileKeys.v1");
    public byte[] Protect(byte[] key) => protector.Protect(key);
    public byte[] Unprotect(FileRecord record) => record.KeyProtected ? protector.Unprotect(record.EncryptionKey) : record.EncryptionKey.ToArray();
}
