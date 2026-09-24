using Microsoft.EntityFrameworkCore;
using SecureShare.Core.Entities;

namespace SecureShare.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // These two lines represent your tables in the database
        public DbSet<FileRecord> FileRecords { get; set; }
        public DbSet<AccessLog> AccessLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FileRecord>().HasIndex(f => f.OwnerId);
            modelBuilder.Entity<AccessLog>().HasIndex(l => l.OperationId).IsUnique().HasFilter("[OperationId] IS NOT NULL");
            modelBuilder.Entity<FileRecord>().HasIndex(f => new { f.IsActive, f.ExpiresAt });
            modelBuilder.Entity<FileRecord>().Property(f => f.OwnerId).HasMaxLength(64);
        }
    }
}
