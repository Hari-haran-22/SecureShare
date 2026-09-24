using Microsoft.Extensions.Options;
using SecureShare.Core.Interfaces;

namespace SecureShare.API.Services;

public class LocalFileStorageService : IFileStorageService
{
    public string RootPath { get; }
    public LocalFileStorageService(IWebHostEnvironment env, IOptions<StorageOptions> options)
    {
        RootPath = Path.GetFullPath(options.Value.Path, env.ContentRootPath);
        Directory.CreateDirectory(RootPath);
    }
    private string Resolve(string name)
    {
        if (string.IsNullOrEmpty(name) || name != Path.GetFileName(name) || name.Contains('/') || name.Contains('\\'))
            throw new IOException("Invalid stored filename.");
        return Path.Combine(RootPath, name);
    }
    public async Task<string> SaveFileAsync(Stream stream, string fileName)
    {
        var name = $"{Guid.NewGuid():N}.enc";
        var path = Resolve(name);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
            await stream.CopyToAsync(output);
            await output.FlushAsync();
            return name;
        }
        catch { File.Delete(path); throw; }
    }
    public Task<Stream> GetFileStreamAsync(string name) => Task.FromResult<Stream>(
        new FileStream(Resolve(name), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, true));
    public Task DeleteFileAsync(string name) { File.Delete(Resolve(name)); return Task.CompletedTask; }
}
