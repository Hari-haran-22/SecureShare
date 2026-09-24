using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SecureShare.API.Services;

namespace SecureShare.API.Controllers;

[ApiController]
[Route("api/session")]
public class SessionController(IAntiforgery antiforgery, IOptions<StorageOptions> storage) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var code = User.FindFirstValue("RecoveryCode");
        if (code == null)
        {
            code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await SignIn(code);
        }
        return Ok(new {
            recoveryCode = code,
            csrfToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken,
            maxFileBytes = storage.Value.MaxFileBytes,
            maxExpiryHours = storage.Value.MaxExpiryHours,
            maxDownloads = storage.Value.MaxDownloads
        });
    }

    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] RecoveryRequest request)
    {
        if (request.Code == null || request.Code.Length != 64 || !request.Code.All(Uri.IsHexDigit))
            return BadRequest(new { detail = "Enter a valid 64-character recovery code." });
        await SignIn(request.Code.ToUpperInvariant());
        return Ok();
    }

    private async Task SignIn(string code)
    {
        var owner = Convert.ToHexString(SHA256.HashData(Convert.FromHexString(code)));
        var identity = new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, owner), new Claim("RecoveryCode", code)
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });
        // Token generation on this request must use the newly established identity.
        HttpContext.User = principal;
    }
    public record RecoveryRequest(string Code);
}
