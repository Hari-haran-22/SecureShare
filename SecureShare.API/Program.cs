using Microsoft.EntityFrameworkCore;
using SecureShare.API.Data;
using SecureShare.API.Services;
using SecureShare.Core.Interfaces;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// --- 1. SETUP DATABASE & SERVICES ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions => 
    {
        // Tells the API to retry up to 5 times, waiting between each attempt, 
        // giving the SQL container plenty of time to boot up!
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
    }));

builder.Services.AddScoped<IFileEncryptionService, FileEncryptionService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

builder.Services.AddControllers();

// --- 2. SETUP SWAGGER UI (The Fix) ---
// We use 'AddSwaggerGen' (Classic), NOT 'AddOpenApi' (New/Raw)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// --- BULLETPROOF DATABASE MIGRATION ---
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    int maxRetries = 6;
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            Console.WriteLine($"[DevOps] Attempting to connect to SQL Server... (Attempt {i + 1}/{maxRetries})");
            dbContext.Database.Migrate(); 
            Console.WriteLine("[DevOps] Database migration successful! SQL Server is ready.");
            break; // Success! Exit the loop.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DevOps] SQL Server not ready yet: {ex.Message}");
            if (i == maxRetries - 1) 
            {
                Console.WriteLine("[DevOps] Fatal error: Could not connect to SQL Server after 60 seconds.");
                throw; // Crash the app if it fails after 1 minute
            }
            
            Console.WriteLine("[DevOps] Waiting 10 seconds before retrying...");
            System.Threading.Thread.Sleep(10000); // Pause the app for 10 seconds
        }
    }
}
// --------------------------------------

// --- 3. ENABLE THE UI ---

// --- 3. ENABLE THE UI ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); // Generates the JSON
    app.UseSwaggerUI(); // Generates the HTML page
}
// ------------------------
app.UseDefaultFiles(); // Looks for index.html
app.UseStaticFiles();  // Serves files from the wwwroot folder
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
// Expose the /metrics endpoint
app.UseMetricServer();

// Automatically track HTTP request duration, errors, etc.
app.UseHttpMetrics();

app.Run();