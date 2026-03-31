using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using DocVersion.Server.Security;
namespace DocVersion.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoginController : ControllerBase
{
    private readonly Dictionary<string, string> users = new Dictionary<string, string>()
{
    { "Alaa", "1234" },
    { "admin", "12345678" },
    { "test-user", "So Long, and Thanks for All the Fish" }
};
    private readonly JwtOptions _jwtOptions;

    public LoginController(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    [HttpPost]
    public IActionResult Login(LoginRequest request)
    {
        if (!users.TryGetValue(request.User, out var password) || password != request.Password)
            return Unauthorized();

        var jwtKey = _jwtOptions.Key
            ?? throw new InvalidOperationException("Jwt:Key is missing in configuration.");
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: new[] { new Claim(ClaimTypes.Name, request.User) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        );
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new { Token = tokenString });
    }
    public record LoginRequest(string User, string Password);
}