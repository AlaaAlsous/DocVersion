using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DocVersion.Server.Security;

public class JwtService
{
    private readonly JwtOptions _jwtOptions;

    public JwtService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public AuthTokens CreateAuthTokens(string email)
    {
        return new AuthTokens(
            AccessToken: CreateToken(email, "access", GetAccessTokenLifetime()),
            RefreshToken: CreateToken(email, "refresh", GetRefreshTokenLifetime())
        );
    }

    public ClaimsPrincipal? ValidateRefreshToken(string refreshToken)
    {
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key)),
            ValidIssuer = _jwtOptions.Issuer,
            ValidAudience = _jwtOptions.Audience,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        var handler = new JwtSecurityTokenHandler();
        try
        {
            return handler.ValidateToken(refreshToken, validationParameters, out _);
        }
        catch
        {
            return null;
        }
    }

    private string CreateToken(string email, string tokenType, TimeSpan lifetime)
    {
        var issuedAt = DateTime.UtcNow;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: new[]
            {
                new Claim("token_type", tokenType),
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Email, email)
            },
            notBefore: issuedAt,
            expires: issuedAt.Add(lifetime),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private TimeSpan GetAccessTokenLifetime()
    {
        var minutes = _jwtOptions.AccessTokenMinutes;
        if (minutes < 5)
        {
            minutes = 5;
        }

        if (minutes > 24 * 60)
        {
            minutes = 24 * 60;
        }

        return TimeSpan.FromMinutes(minutes);
    }

    private TimeSpan GetRefreshTokenLifetime()
    {
        var days = _jwtOptions.RefreshTokenDays;
        if (days < 1)
        {
            days = 1;
        }

        if (days > 90)
        {
            days = 90;
        }

        return TimeSpan.FromDays(days);
    }
}

public record AuthTokens(string AccessToken, string RefreshToken)
{
    public string Token => AccessToken;
}