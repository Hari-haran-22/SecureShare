using System;
using System.Collections.Generic;

namespace SecureShare.Core.Entities
{
    public class FileRecord
    {
        public Guid Id { get; set; } // The unique ID for the download link
        public string OriginalFilename { get; set; } = string.Empty;
        public string StoredFilename { get; set; } = string.Empty; // The random name on disk
        public DateTime UploadedAt { get; set; }
        public DateTime? ExpiresAt { get; set; } // Nullable: file might not expire by time
        public int? MaxDownloads { get; set; } // Nullable: might have unlimited downloads
        public int DownloadCount { get; set; }
        public bool IsActive { get; set; }

        public byte[] EncryptionKey { get; set; } = Array.Empty<byte>();
        public byte[] IV { get; set; } = Array.Empty<byte>();

        // Navigation property for the relationship
        public ICollection<AccessLog> AccessLogs { get; set; } = new List<AccessLog>();
    }
}
