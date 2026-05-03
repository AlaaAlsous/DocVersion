using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Net.Mail;
using DocVersion.Server.Data;
using DocVersion.Server.Models;
using DocVersion.Server.Security;

namespace DocVersion.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class LoginController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;
    private readonly JwtService _jwtService;

    public LoginController(
        AppDbContext db,
        IPasswordHasher<UserAccount> passwordHasher,
        JwtService jwtService
    )
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromBody] AuthRequest request)
    {
        var email = NormalizeEmail(request.Email);
        if (email is null || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and password are required." });
        }

        var user = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Email == email);
        if (user is null)
        {
            return Unauthorized();
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        return Ok(CreateAuthResponse(user));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] AuthRequest request)
    {
        var email = NormalizeEmail(request.Email);
        if (email is null)
        {
            return BadRequest(new { message = "Email must be a valid email address." });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return BadRequest(new { message = "Password must be at least 8 characters." });
        }

        var exists = await _db.UserAccounts.AnyAsync(x => x.Email == email);
        if (exists)
        {
            return Conflict(new { message = "User already exists." });
        }

        var user = new UserAccount
        {
            Email = email,
            PasswordHash = string.Empty
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.UserAccounts.Add(user);
        await _db.SaveChangesAsync();

        return Ok(CreateAuthResponse(user));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized();
        }

        var principal = _jwtService.ValidateRefreshToken(refreshToken);
        if (principal is null)
        {
            return Unauthorized();
        }

        var tokenType = principal.FindFirstValue("token_type");
        if (!string.Equals(tokenType, "refresh", StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var email = NormalizeEmail(principal.FindFirstValue(ClaimTypes.Email));
        if (email is null)
        {
            return Unauthorized();
        }

        var user = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Email == email);
        if (user is null)
        {
            return Unauthorized();
        }

        var versionClaim = principal.FindFirstValue("rtv");
        if (!int.TryParse(versionClaim, out var tokenVersion) || tokenVersion != user.RefreshTokenVersion)
        {
            return Unauthorized();
        }

        return Ok(CreateAuthResponse(user));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var email = NormalizeEmail(User.FindFirstValue(ClaimTypes.Email));
        if (email is null)
        {
            return Unauthorized();
        }

        var user = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Email == email);
        if (user is null)
        {
            return Unauthorized();
        }

        user.RefreshTokenVersion += 1;
        await _db.SaveChangesAsync();

        ClearRefreshCookie();
        return Ok();
    }

    private object CreateAuthResponse(UserAccount user)
    {
        var tokens = _jwtService.CreateAuthTokens(user.Email, user.RefreshTokenVersion);
        SetRefreshCookie(tokens.RefreshToken);
        return new { Token = tokens.Token };
    }

    private void SetRefreshCookie(string refreshToken)
    {
        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(_jwtService.GetRefreshTokenDays()),
            Path = "/api/login"
        });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            Path = "/api/login",
            SameSite = SameSiteMode.Strict,
            Secure = HttpContext.Request.IsHttps,
            HttpOnly = true
        });
    }

    private static string? NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = value.Trim();
        try
        {
            var parsed = new MailAddress(candidate);
            if (!string.Equals(parsed.Address, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return parsed.Address.ToLowerInvariant();
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public record AuthRequest(string Email, string Password);
}