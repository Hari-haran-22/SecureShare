using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureShare.API.Data;
using SecureShare.Core.Entities;
using SecureShare.Core.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SecureShare.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileEncryptionService _encryptionService;
        private readonly IFileStorageService _storageService;

        public FilesController(
            ApplicationDbContext context,
            IFileEncryptionService encryptionService,
            IFileStorageService storageService)
        {
            _context = context;
            _encryptionService = encryptionService;
            _storageService = storageService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] int? expiryHours, [FromForm] int? maxDownloads)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            // 1. Encrypt the file stream
            using var fileStream = file.OpenReadStream();
            using var encryptedStream = new MemoryStream();

            // Encrypt and get the keys
            var (key, iv) = await _encryptionService.EncryptAsync(fileStream, encryptedStream);

            // 2. Save the encrypted file to disk
            encryptedStream.Position = 0; // Reset stream to beginning before saving
            var storedFileName = await _storageService.SaveFileAsync(encryptedStream, file.FileName);

            // 3. Save metadata to Database
            var fileRecord = new FileRecord
            {
                Id = Guid.NewGuid(),
                OriginalFilename = file.FileName,
                StoredFilename = storedFileName,
                UploadedAt = DateTime.UtcNow,
                ExpiresAt = expiryHours.HasValue ? DateTime.UtcNow.AddHours(expiryHours.Value) : null,
                MaxDownloads = maxDownloads,
                EncryptionKey = key,
                IV = iv,
                IsActive = true
            };

            _context.FileRecords.Add(fileRecord);
            await _context.SaveChangesAsync();

            // 4. Return the unique download URL
            var downloadUrl = $"{Request.Scheme}://{Request.Host}/api/files/{fileRecord.Id}";
            return Ok(new { url = downloadUrl, expiresAt = fileRecord.ExpiresAt });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            // 1. Find the file record
            var record = await _context.FileRecords.FirstOrDefaultAsync(f => f.Id == id);

            // 2. Security Checks
            if (record == null || !record.IsActive)
                return NotFound("File not found or has expired.");

            if (record.ExpiresAt.HasValue && record.ExpiresAt < DateTime.UtcNow)
            {
                record.IsActive = false;
                await _context.SaveChangesAsync();
                return NotFound("File has expired.");
            }

            if (record.MaxDownloads.HasValue && record.DownloadCount >= record.MaxDownloads)
            {
                record.IsActive = false;
                await _context.SaveChangesAsync();
                return NotFound("Download limit reached.");
            }

            // 3. Log the Access
            // In a real app, you'd get the IP from HttpContext.Connection.RemoteIpAddress
            var log = new AccessLog
            {
                FileRecordId = record.Id,
                AccessedAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1",
                UserAgent = Request.Headers["User-Agent"].ToString()
            };
            _context.AccessLogs.Add(log);

            // 4. Increment Download Count
            record.DownloadCount++;
            if (record.MaxDownloads.HasValue && record.DownloadCount >= record.MaxDownloads)
            {
                record.IsActive = false; // Mark as inactive immediately after final download
            }
            await _context.SaveChangesAsync();

            // 5. Decrypt and Return File
            var encryptedStream = await _storageService.GetFileStreamAsync(record.StoredFilename);
            var decryptedStream = new MemoryStream();

            await _encryptionService.DecryptAsync(encryptedStream, decryptedStream, record.EncryptionKey, record.IV);

            decryptedStream.Position = 0; // Reset for reading
            return File(decryptedStream, "application/octet-stream", record.OriginalFilename);
        }
    }
}