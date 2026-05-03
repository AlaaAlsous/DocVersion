using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using DocVersion.Server.Data;
using DocVersion.Server.Models;
using DocVersion.Server.Security;

namespace DocVersion.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoginController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;
    private readonly JwtOptions _jwtOptions;

    public LoginController(
        AppDbContext db,
        IPasswordHasher<UserAccount> passwordHasher,
        IOptions<JwtOptions> jwtOptions
    )
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtOptions = jwtOptions.Value;
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

        return Ok(new { Token = CreateToken(user.Email) });
    }

    private string CreateToken(string email)
    {
        var jwtKey = _jwtOptions.Key
            ?? throw new InvalidOperationException("Jwt:Key is missing in configuration.");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: new[]
            {
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Email, email)
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
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