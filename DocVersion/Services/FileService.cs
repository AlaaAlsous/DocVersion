using DocVersion.Models;

namespace DocVersion.Services;

public class FileService
{
    private readonly string _storagePath;
    public FileService()
    {
        _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "Storage");
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public Task<Dictionary<string, FileMetadata>> GetAllFilesAsync(string username)
    {
        var userPath = Path.Combine(_storagePath, username);
        if (!Directory.Exists(userPath))
        {
            Directory.CreateDirectory(userPath);
        }

        var result = new Dictionary<string, FileMetadata>();
        var files = Directory.GetFiles(userPath);
        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            var metadata = new FileMetadata
            {
                Created = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Changed = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                IsFile = true,
                Bytes = fileInfo.Length,
                Extension = fileInfo.Extension
            };
            result[fileInfo.Name] = metadata;
        }
        return Task.FromResult(result);
    }
}