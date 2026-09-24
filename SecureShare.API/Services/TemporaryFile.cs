namespace SecureShare.API.Services;

public static class TemporaryFile
{
    public static FileStream Create() => new(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"secureshare-{Guid.NewGuid():N}.tmp"),
        FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536,
        FileOptions.Asynchronous | FileOptions.DeleteOnClose);
}
