using Microsoft.EntityFrameworkCore;

namespace DocVersion.Server.Models;

[Index(nameof(Email), IsUnique = true)]
public class UserAccount
{
    public long Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public int RefreshTokenVersion { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}