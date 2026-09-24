namespace SecureShare.API.Services;

public class StorageOptions
{
    public string Path { get; set; } = "SecureUploads";
    public long MaxFileBytes { get; set; } = 100 * 1024 * 1024;
    public long OwnerQuotaBytes { get; set; } = 500 * 1024 * 1024;
    public long TotalQuotaBytes { get; set; } = 5L * 1024 * 1024 * 1024;
    public int MaxFilesPerOwner { get; set; } = 100;
    public int MaxTotalFiles { get; set; } = 10000;
    public int MaxExpiryHours { get; set; } = 168;
    public int MaxDownloads { get; set; } = 100;
    public int CleanupSeconds { get; set; } = 60;
    public int AuditRetentionDays { get; set; } = 30;
}
