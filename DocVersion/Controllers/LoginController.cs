using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;


namespace DocVersion.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoginController : ControllerBase
{
    private readonly Dictionary<string, string> users = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    { "Alaa", "1234" },
    { "admin", "12345678" },
    { "test-user", "So Long, and Thanks for All the Fish" }
};
    private readonly IConfiguration _configuration;

    public LoginController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost]
    public IActionResult Login(LoginRequest request)
    {
        if (!users.TryGetValue(request.User, out var password) || password != request.Password)
            return Unauthorized();

        var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
            ?? _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT key not found in environment variables or configuration.");
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var token = new JwtSecurityToken(
            claims: new[] { new Claim(ClaimTypes.Name, request.User) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        );
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new { Token = tokenString });
    }
    public record LoginRequest(string User, string Password);
}