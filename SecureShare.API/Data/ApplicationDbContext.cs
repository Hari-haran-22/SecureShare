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
    }
}