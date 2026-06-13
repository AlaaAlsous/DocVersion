namespace DocVersion.Server.Models;

public class BinItem
{
    public long Id { get; set; }
    public required string Username { get; set; }
    public required string OriginalPath { get; set; }
    public required string StoragePath { get; set; }
    public bool IsFile { get; set; }
    public long SizeBytes { get; set; }
    public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}