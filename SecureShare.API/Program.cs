using Microsoft.EntityFrameworkCore;
using SecureShare.API.Data;
using SecureShare.API.Services;
using SecureShare.Core.Interfaces;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// --- 1. SETUP DATABASE & SERVICES ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IFileEncryptionService, FileEncryptionService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

builder.Services.AddControllers();

// --- 2. SETUP SWAGGER UI (The Fix) ---
// We use 'AddSwaggerGen' (Classic), NOT 'AddOpenApi' (New/Raw)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// --- AUTOMATIC DATABASE MIGRATION (The Missing Piece!) ---
// This ensures the empty SQL container gets all your tables built when it starts.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate(); 
}
// --

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