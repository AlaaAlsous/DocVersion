using Microsoft.AspNetCore.StaticFiles;
using DocVersion.Models;
using DocVersion.Data;
using Microsoft.EntityFrameworkCore;
namespace DocVersion.Services;

public class FileService
{
    private readonly string _storagePath;
    private readonly string _historyStoragePath;
    private readonly AppDbContext _dbContext;
    public FileService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "Storage");
        _historyStoragePath = Path.Combine(_storagePath, ".history");
        if (!Directory.Exists(_storagePath))
            Directory.CreateDirectory(_storagePath);
        if (!Directory.Exists(_historyStoragePath))
            Directory.CreateDirectory(_historyStoragePath);
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

        await SaveFileVersionAsync(username, filename, userPath);
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

        if (File.Exists(userPath))
        {
            await SaveFileVersionAsync(username, filename, userPath);
        }
        using var fileStream = new FileStream(userPath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream);
    }

    private async Task SaveFileVersionAsync(string username, string filename, string sourceFilePath)
    {
        if (!File.Exists(sourceFilePath)) return;

        var lastVersion = await _dbContext.FileHistories
            .Where(f => f.Username == username && f.FilePath == filename)
            .OrderByDescending(f => f.Version)
            .Select(f => f.Version)
            .FirstOrDefaultAsync();

        var nextVersion = lastVersion + 1;
        var versionDirectory = GetSafeHistoryDirectoryPath(username, filename);
        if (!Directory.Exists(versionDirectory))
            Directory.CreateDirectory(versionDirectory);

        var versionFilePath = Path.Combine(versionDirectory, $"{nextVersion}.bin");
        await CopyFileAsync(sourceFilePath, versionFilePath);

        var versionFileInfo = new FileInfo(versionFilePath);

        var newVersion = new FileHistory
        {
            Username = username,
            FilePath = filename,
            Version = nextVersion,
            StoragePath = GetRelativeHistoryPath(versionFilePath),
            SizeBytes = versionFileInfo.Length,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.FileHistories.Add(newVersion);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<FileHistory>> GetFileHistoryAsync(string username, string filename)
    {
        return await _dbContext.FileHistories
            .Where(f => f.Username == username && f.FilePath == filename)
            .OrderByDescending(f => f.Version)
            .ToListAsync();
    }

    public async Task RestoreFileHistoryAsync(string username, string filename, int version)
    {
        var fileVersion = await _dbContext.FileHistories
            .Where(f => f.Username == username && f.FilePath == filename && f.Version == version)
            .FirstOrDefaultAsync();

        if (fileVersion == null)
            throw new InvalidOperationException("File version not found.");

        var userPath = GetSafePath(username, filename);
        var directory = Path.GetDirectoryName(userPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory!);

        var versionFilePath = GetAbsoluteHistoryPath(fileVersion.StoragePath);
        if (!File.Exists(versionFilePath))
            throw new InvalidOperationException("Stored file version not found.");

        if (File.Exists(userPath))
        {
            await SaveFileVersionAsync(username, filename, userPath);
        }

        await CopyFileAsync(versionFilePath, userPath);
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

    private string GetSafeHistoryDirectoryPath(string username, string filename)
    {
        var historyPath = Path.Combine(_historyStoragePath, username);
        var fullPath = Path.GetFullPath(Path.Combine(historyPath, filename));
        var fullHistoryPath = Path.GetFullPath(historyPath);
        if (fullPath != fullHistoryPath && !fullPath.StartsWith(fullHistoryPath + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Invalid history path.");
        return fullPath;
    }

    private string GetRelativeHistoryPath(string absoluteHistoryPath)
    {
        return Path.GetRelativePath(_historyStoragePath, absoluteHistoryPath);
    }

    private string GetAbsoluteHistoryPath(string relativeHistoryPath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_historyStoragePath, relativeHistoryPath));
        var fullHistoryPath = Path.GetFullPath(_historyStoragePath);
        if (!fullPath.StartsWith(fullHistoryPath + Path.DirectorySeparatorChar) && fullPath != fullHistoryPath)
            throw new InvalidOperationException("Invalid history path.");
        return fullPath;
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath)
    {
        using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await sourceStream.CopyToAsync(destinationStream);
    }
}