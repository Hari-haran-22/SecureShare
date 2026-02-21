using System;

namespace SecureShare.Core.Entities
{
    public class AccessLog
    {
        public int Id { get; set; } // Internal ID, doesn't need to be exposed
        public Guid FileRecordId { get; set; } // Foreign Key
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty; // Browser details
        public DateTime AccessedAt { get; set; }

        // Navigation property back to the file
        public FileRecord? FileRecord { get; set; }
    }
}