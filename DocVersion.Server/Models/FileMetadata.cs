using System.Text.Json.Serialization;
namespace DocVersion.Models;

public class FileMetadata
{
    [JsonPropertyName("created")]
    public string Created { get; set; } = "";
    [JsonPropertyName("changed")]
    public string Changed { get; set; } = "";
    [JsonPropertyName("file")]
    public bool IsFile { get; set; }
    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }
    [JsonPropertyName("extension")]
    public string? Extension { get; set; }
    [JsonPropertyName("content")]
    public Dictionary<string, FileMetadata>? Content { get; set; }
}