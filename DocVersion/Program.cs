using System.Text;
using Microsoft.IdentityModel.Tokens;
;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
          ?? builder.Configuration["Jwt:Key"]
          ?? throw new InvalidOperationException("JWT key not found in environment variables or configuration.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run("http://localhost:3000/");
