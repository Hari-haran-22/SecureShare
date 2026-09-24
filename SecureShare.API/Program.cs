using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using SecureShare.API.Data;
using SecureShare.API.Services;
using SecureShare.Core.Entities;
using SecureShare.Core.Interfaces;

if (args.Contains("--check-health"))
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var response = await client.GetAsync("http://127.0.0.1:8080/health/ready");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch { Environment.ExitCode = 1; }
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
var local = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
var connection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection.");
var databaseProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
builder.Services.AddDbContext<ApplicationDbContext>(o =>
{
    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        o.UseSqlite(connection);
    else if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        o.UseSqlServer(connection, sql => sql.EnableRetryOnFailure());
    else
        throw new InvalidOperationException("Database:Provider must be SqlServer or Sqlite.");
});
builder.Services.AddOptions<StorageOptions>().BindConfiguration("Storage")
    .Validate(o => o.MaxFileBytes > 0 && o.MaxFileBytes <= 1024L * 1024 * 1024 &&
        o.OwnerQuotaBytes >= o.MaxFileBytes && o.TotalQuotaBytes >= o.OwnerQuotaBytes &&
        o.MaxFilesPerOwner > 0 && o.MaxTotalFiles >= o.MaxFilesPerOwner && o.MaxExpiryHours > 0 && o.MaxDownloads > 0 &&
        o.CleanupSeconds >= 10 && o.AuditRetentionDays > 0, "Invalid storage limits.").ValidateOnStart();
var maxFile = builder.Configuration.GetValue<long>("Storage:MaxFileBytes", 100 * 1024 * 1024);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxFile + 1024 * 1024);
builder.Services.Configure<FormOptions>(o => {
    o.MultipartBodyLengthLimit = maxFile + 1024 * 1024;
    o.MemoryBufferThreshold = 65536;
    o.ValueLengthLimit = 4096;
});
var keyPath = Path.GetFullPath(builder.Configuration["DataProtection:KeyPath"] ?? "DataProtectionKeys",
    builder.Environment.ContentRootPath);
Directory.CreateDirectory(keyPath);
var protection = builder.Services.AddDataProtection().SetApplicationName("SecureShare")
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath));
var certPath = builder.Configuration["DataProtection:CertificatePath"];
if (!string.IsNullOrWhiteSpace(certPath))
{
    var password = builder.Configuration["DataProtection:CertificatePassword"];
    var passwordFile = builder.Configuration["DataProtection:CertificatePasswordFile"];
    if (passwordFile != null) password = File.ReadAllText(passwordFile).TrimEnd('\r', '\n');
    var cert = X509CertificateLoader.LoadPkcs12FromFile(certPath, password,
        X509KeyStorageFlags.EphemeralKeySet);
    protection.ProtectKeysWithCertificate(cert);
}
else if (!local) throw new InvalidOperationException("Production requires a DataProtection certificate.");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => {
    o.Cookie.Name = "SecureShare.Owner";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = local ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromDays(30);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o => {
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "SecureShare.Csrf";
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = local ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IFileEncryptionService, FileEncryptionService>();
builder.Services.AddScoped<LocalFileStorageService>();
builder.Services.AddScoped<IFileStorageService>(s => s.GetRequiredService<LocalFileStorageService>());
builder.Services.AddScoped<FileKeyProtector>();
builder.Services.AddScoped<FileScanner>();
builder.Services.AddScoped<IFileScanner>(s => s.GetRequiredService<FileScanner>());
builder.Services.AddScoped<FileAccessService>();
builder.Services.AddScoped<LegacyFileUpgrade>();
builder.Services.Configure<PasswordHasherOptions>(o => o.IterationCount = 210000);
builder.Services.AddScoped<IPasswordHasher<FileRecord>, PasswordHasher<FileRecord>>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<ScannerHealthCheck>("scanner", tags: ["ready"])
    .AddCheck<StorageHealthCheck>("storage", tags: ["ready"]);
builder.Services.AddHostedService<FileCleanupService>();
builder.Services.Configure<ForwardedHeadersOptions>(o => {
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        o.KnownProxies.Add(IPAddress.Parse(proxy));
});
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.OnRejected = async (context, token) => {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new { detail = "Too many requests. Try again in a minute." }, token);
    };
    o.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })),
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            context.Request.Path.StartsWithSegments("/health") || context.Request.Path == "/metrics"
                ? RateLimitPartition.GetNoLimiter("health")
                : RateLimitPartition.GetConcurrencyLimiter("server",
                    _ => new ConcurrencyLimiterOptions { PermitLimit = 8, QueueLimit = 0 })));
    o.AddPolicy("upload", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("download", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
if (args.Contains("--check-database"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.OpenConnectionAsync();
    await using var command = db.Database.GetDbConnection().CreateCommand();
    command.CommandText = db.Database.IsSqlite() ? "PRAGMA integrity_check;" : "SELECT 1";
    var result = await command.ExecuteScalarAsync();
    if (db.Database.IsSqlite() && !string.Equals(result?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("SQLite integrity check failed.");
    return;
}
if (builder.Configuration.GetValue("Database:MigrateOnStartup", local) || args.Contains("--migrate-only"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (db.Database.IsSqlite())
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<LegacyFileUpgrade>().RunAsync();
}
if (args.Contains("--migrate-only")) return;

app.UseForwardedHeaders();
app.UseExceptionHandler();
if (!local)
{
    app.UseHsts();
    app.UseWhen(c => !c.Request.Path.StartsWithSegments("/health") && c.Request.Path != "/metrics",
        branch => branch.UseHttpsRedirection());
}
app.Use(async (context, next) => {
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    if (!(app.Environment.IsDevelopment() && context.Request.Path.StartsWithSegments("/swagger")))
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api"))
        context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseRouting();
app.UseHttpMetrics();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });
app.MapMetrics();
app.MapControllers();
await app.RunAsync();

public partial class Program { }
