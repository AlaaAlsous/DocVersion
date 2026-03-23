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
        var userPath = GetSafePath(username, "");
        if (!Directory.Exists(userPath))
            Directory.CreateDirectory(userPath);

        var result = GetFolderContent(userPath);
        return Task.FromResult(result);
    }

    public Task<FileMetadata?> GetFileMetadataAsync(string username, string filename)
    {
        var userPath = GetSafePath(username, filename);

        if (File.Exists(userPath))
        {
            var fileInfo = new FileInfo(userPath);
            var metadata = new FileMetadata
            {
                Created = fileInfo.CreationTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                Changed = fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                IsFile = true,
                Bytes = fileInfo.Length,
                Extension = fileInfo.Extension
            };
            return Task.FromResult<FileMetadata?>(metadata);
        }

        if (Directory.Exists(userPath))
        {
            var dirInfo = new DirectoryInfo(userPath);
            var metadata = new FileMetadata
            {
                Created = dirInfo.CreationTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                Changed = dirInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                IsFile = false,
                Bytes = CalculateDirectorySize(userPath),
                Extension = null
            };
            return Task.FromResult<FileMetadata?>(metadata);
        }

        return Task.FromResult<FileMetadata?>(null);
    }

    public Task<(Stream, string)> GetFileContentAsync(string username, string filename)
    {
        var userPath = GetSafePath(username, filename);
        if (!File.Exists(userPath))
            return Task.FromResult<(Stream, string)>((null!, null!));

        var fileStream = new FileStream(userPath, FileMode.Open, FileAccess.Read);

        var contentType = "application/octet-stream";
        return Task.FromResult<(Stream, string)>((fileStream, contentType));
    }

    public Task<Dictionary<string, FileMetadata>?> GetFolderContentAsync(string username, string foldername)
    {
        var userPath = GetSafePath(username, foldername);
        if (!Directory.Exists(userPath))
            return Task.FromResult<Dictionary<string, FileMetadata>?>(null);

        return Task.FromResult<Dictionary<string, FileMetadata>?>(GetFolderContent(userPath));
    }

    private Dictionary<string, FileMetadata> GetFolderContent(string folderName)
    {
        var result = new Dictionary<string, FileMetadata>();
        var files = Directory.GetFiles(folderName);
        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            var metadata = new FileMetadata
            {
                Created = fileInfo.CreationTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                Changed = fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                IsFile = true,
                Bytes = fileInfo.Length,
                Extension = fileInfo.Extension
            };
            result[fileInfo.Name] = metadata;
        }
        var folders = Directory.GetDirectories(folderName);
        foreach (var folder in folders)
        {
            var folderInfo = new DirectoryInfo(folder);
            var metadata = new FileMetadata
            {
                Created = folderInfo.CreationTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                Changed = folderInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                IsFile = false,
                Bytes = CalculateDirectorySize(folder),
                Extension = null,
                Content = GetFolderContent(folder)
            };
            result[folderInfo.Name] = metadata;
        }
        return result;
    }

    private static long CalculateDirectorySize(string folder)
    {
        long totalBytes = 0;

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            try
            {
                totalBytes += new FileInfo(file).Length;
            }
            catch { }
        }

        return totalBytes;
    }

    public async Task<bool> CreateFileAsync(string username, string filename, Stream content)
    {
        var userPath = GetSafePath(username, filename);
        var directory = Path.GetDirectoryName(userPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory!);
        if (File.Exists(userPath)) return false;
        using var fileStream = new FileStream(userPath, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(fileStream);
        return true;
    }

    public Task<bool> CreateFolderAsync(string username, string foldername)
    {
        var userPath = GetSafePath(username, foldername);
        if (Directory.Exists(userPath) || File.Exists(userPath))
            return Task.FromResult(false);

        Directory.CreateDirectory(userPath);
        return Task.FromResult(true);
    }

    public async Task SaveFileAsync(string username, string filename, Stream content)
    {
        var userPath = GetSafePath(username, filename);
        var directory = Path.GetDirectoryName(userPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory!);
        using var fileStream = new FileStream(userPath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream);
    }

    public Task<bool> DeleteFileAsync(string username, string filename)
    {
        var userPath = GetSafePath(username, filename);
        if (!File.Exists(userPath))
            return Task.FromResult(false);
        File.SetAttributes(userPath, FileAttributes.Normal);
        File.Delete(userPath);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteFolderAsync(string username, string foldername)
    {
        var userPath = GetSafePath(username, foldername);
        if (!Directory.Exists(userPath))
            return Task.FromResult(false);
        PrepareDirectoryForDelete(userPath);
        Directory.Delete(userPath, recursive: true);
        return Task.FromResult(true);
    }

    private static void PrepareDirectoryForDelete(string folder)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
            catch { }
        }

        try
        {
            File.SetAttributes(folder, FileAttributes.Normal);
        }
        catch { }
    }

    public Task<bool> FileExistsAsync(string username, string filename)
    {
        var userPath = GetSafePath(username, filename);
        return Task.FromResult(File.Exists(userPath));
    }

    public Task<bool> FolderExistsAsync(string username, string foldername)
    {
        var userPath = GetSafePath(username, foldername);
        return Task.FromResult(Directory.Exists(userPath));
    }

    private string GetSafePath(string username, string filename)
    {
        var userPath = Path.Combine(_storagePath, username);
        var fullPath = Path.GetFullPath(Path.Combine(userPath, filename));
        var fullUserPath = Path.GetFullPath(userPath);
        if (fullPath != fullUserPath && !fullPath.StartsWith(fullUserPath + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Invalid file path.");
        return fullPath;
    }
}