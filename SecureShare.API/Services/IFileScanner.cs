namespace SecureShare.API.Services;

public interface IFileScanner
{
    Task<bool> IsCleanAsync(Stream stream, CancellationToken cancellationToken);
}
