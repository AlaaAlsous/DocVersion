using Microsoft.EntityFrameworkCore;

namespace DocVersion.Models;

[Index(nameof(Username), nameof(FilePath))]
[Index(nameof(Username), nameof(FilePath), nameof(Version), IsUnique = true)]
public class FileHistory
{
    public long Id { get; set; }
    public required string Username { get; set; }
    public required string FilePath { get; set; }
    public int Version { get; set; }
    public required byte[] Content { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

}