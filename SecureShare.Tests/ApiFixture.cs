using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SecureShare.API.Data;

namespace SecureShare.Tests;

public class ApiFixture : WebApplicationFactory<Program>
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "secureshare-tests-" + Guid.NewGuid().ToString("N"));
    public ApiFixture() => Directory.CreateDirectory(Root);
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?> {
            ["Database:MigrateOnStartup"] = "false",
            ["Storage:Path"] = Path.Combine(Root, "uploads"),
            ["DataProtection:KeyPath"] = Path.Combine(Root, "keys"),
            ["Scanner:Enabled"] = "false"
        }));
        builder.ConfigureServices(services => {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite("Data Source=" + Path.Combine(Root, "test.db") + ";Default Timeout=30"));
            foreach (var hosted in services.Where(s => s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType == typeof(SecureShare.API.Services.FileCleanupService)).ToArray())
                services.Remove(hosted);
        });
    }
    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            // Only this fixture's explicitly created temporary directory is removed.
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
