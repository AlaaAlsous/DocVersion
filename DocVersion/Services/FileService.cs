using System.Security.Claims;
using Microsoft.AspNetCore.StaticFiles;
using DocVersion.Models;


namespace DocVersion.Services;

public class FileService
{
    private readonly string _storagePath;
    public FileService()
    {
        _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "Storage");
        if (!Directory.Exists(_storagePath))
            Directory.CreateDirectory(_storagePath);

    }

    public Task<Dictionary<string, FileMetadata>> GetAllFilesAsync(string username)
    {
        var userPath = Path.Combine(_storagePath, username);
        if (!Directory.Exists(userPath))
            Directory.CreateDirectory(userPath);

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

    public Task<FileMetadata?> GetFileMetadataAsync(string username, string filename)
    {
        var userPath = Path.Combine(_storagePath, username, filename);
        if (!File.Exists(userPath))
            return Task.FromResult<FileMetadata?>(null);

        var fileInfo = new FileInfo(userPath);

        var metadata = new FileMetadata
        {
            Created = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
            Changed = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
            IsFile = true,
            Bytes = fileInfo.Length,
            Extension = fileInfo.Extension
        };
        return Task.FromResult<FileMetadata?>(metadata);
    }

    public Task<(Stream, string)> GetFileContentAsync(string username, string filename)
    {
        var userPath = Path.Combine(_storagePath, username, filename);
        if (!File.Exists(userPath))
            return Task.FromResult<(Stream, string)>((null!, null!));

        var fileStream = new FileStream(userPath, FileMode.Open, FileAccess.Read);

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(filename, out var contentType))
            contentType = "application/octet-stream";
        return Task.FromResult<(Stream, string)>((fileStream, contentType));
    }

    public async Task<bool> CreateFileAsync(string username, string filename, Stream content)
    {
        var userPath = Path.Combine(_storagePath, username, filename);
        var directory = Path.GetDirectoryName(userPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory!);
        if (File.Exists(userPath)) return false;
        using var fileStream = new FileStream(userPath, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(fileStream);
        return true;
    }

    public async Task SaveFileAsync(string username, string filename, Stream content)
    {
        var userPath = Path.Combine(_storagePath, username, filename);
        var directory = Path.GetDirectoryName(userPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory!);
        using var fileStream = new FileStream(userPath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream);
    }
}