using Microsoft.AspNetCore.StaticFiles;
using DocVersion.Core.Models;
using DocVersion.Server.Models;
using DocVersion.Server.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR;
using DocVersion.Server.Hubs;
namespace DocVersion.Server.Services;

using System.IO.Compression;

public class FileService
{
    private readonly AppDbContext _dbContext;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private readonly IHubContext<EventsHub> _hub;
    private readonly BlobStorageService _blob;

    public FileService(AppDbContext dbContext, IHubContext<EventsHub> hub, BlobStorageService blob)
    {
        _dbContext = dbContext;
        _hub = hub;
        _blob = blob;
    }

    private static readonly HashSet<string> IgnoredFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "desktop.ini",
        ".folder",
        "thumbs.db",
        ".ds_store",
        "ehthumbs.db",
        "icon\r",
        "icon\r\n",
        "__folder_placeholder"
    };

    private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "$recycle.bin",
        "system volume information"
    };

    private static bool ShouldIgnoreFile(string filename)
    {
        var name = Path.GetFileName(filename);
        return IgnoredFiles.Contains(name);
    }

    private static bool ShouldIgnoreFolder(string folderName)
    {
        return IgnoredFolders.Contains(folderName);
    }

    private static string NormalizePath(string path)
    {
        path = path.Replace("\\", "/").TrimStart('/');

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && parts[0].Contains("@"))
        {
            parts = parts.Skip(1).ToArray();
            path = string.Join("/", parts);
        }

        return path;
    }

    private static string ComputeSha256Hash(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(ms);

        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public async Task<Dictionary<string, FileMetadata>> GetAllFilesAsync(string username)
    {
        var list = await _blob.ListFolderAsync(username, null);
        var dict = new Dictionary<string, FileMetadata>();

        foreach (var item in list)
        {
            if (ShouldIgnoreFile(item.Name))
                continue;

            long bytes = item.Bytes;
            if (!item.IsFile)
            {
                var allInFolder = await _blob.ListAllFilesRecursiveAsync(username, item.Name);
                bytes = 0;
                foreach (var blobName in allInFolder)
                {
                    var blobProps = await _blob.GetPropertiesAsync(username, blobName.Substring($"{username}/".Length));
                    bytes += blobProps?.ContentLength ?? 0;
                }
            }
            var ext = item.IsFile ? Path.GetExtension(item.Name) : null;
            dict[item.Name] = new FileMetadata
            {
                IsFile = item.IsFile,
                Bytes = bytes,
                Extension = ext,
                Created = (item.Created ?? DateTimeOffset.UtcNow).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Changed = (item.Modified ?? DateTimeOffset.UtcNow).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        return dict;
    }

    public async Task<FileMetadata?> GetFileMetadataAsync(string username, string filename)
    {
        filename = NormalizePath(filename);

        if (ShouldIgnoreFile(filename))
            return null;

        var (stream, props) = await _blob.DownloadAsync(username, filename);
        if (stream != null && props != null)
        {
            await stream.DisposeAsync();
            return new FileMetadata
            {
                IsFile = true,
                Bytes = props.ContentLength,
                Extension = Path.GetExtension(filename),
                Created = props.CreatedOn.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Changed = props.LastModified.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        var allInFolder = await _blob.ListAllFilesRecursiveAsync(username, filename);
        if (allInFolder.Count > 0)
        {
            long totalBytes = 0;
            DateTimeOffset? created = null;
            DateTimeOffset? modified = null;
            foreach (var blobName in allInFolder)
            {
                var blobProps = await _blob.GetPropertiesAsync(username, blobName.Substring($"{username}/".Length));
                totalBytes += blobProps?.ContentLength ?? 0;
                if (blobProps != null)
                {
                    if (created == null || blobProps.CreatedOn < created)
                        created = blobProps.CreatedOn;
                    if (modified == null || blobProps.LastModified > modified)
                        modified = blobProps.LastModified;
                }
            }
            var folderCreated = created ?? DateTimeOffset.UtcNow;
            var folderChanged = modified ?? folderCreated;

            return new FileMetadata
            {
                IsFile = false,
                Bytes = totalBytes,
                Extension = null,
                Created = folderCreated.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Changed = folderChanged.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        return null;
    }

    public async Task<(Stream, string)> GetFileContentAsync(string username, string filename)
    {
        filename = NormalizePath(filename);

        if (ShouldIgnoreFile(filename))
            return (null!, null!);

        var (stream, props) = await _blob.DownloadAsync(username, filename);
        if (stream == null)
            return (null!, null!);

        var contentType = GetContentType(filename);
        return (stream, contentType);
    }

    public async Task<Dictionary<string, FileMetadata>?> GetFolderContentAsync(string username, string foldername)
    {
        foldername = NormalizePath(foldername);

        if (ShouldIgnoreFolder(foldername))
            return null;

        var list = await _blob.ListFolderAsync(username, foldername);
        if (list.Count == 0) return null;

        var dict = new Dictionary<string, FileMetadata>();
        foreach (var item in list)
        {
            if (ShouldIgnoreFile(item.Name))
                continue;

            long bytes = item.Bytes;
            if (!item.IsFile)
            {
                var fullPath = foldername + "/" + item.Name;
                var allInSubfolder = await _blob.ListAllFilesRecursiveAsync(username, fullPath);
                bytes = 0;
                foreach (var blobName in allInSubfolder)
                {
                    var blobProps = await _blob.GetPropertiesAsync(username, blobName.Substring($"{username}/".Length));
                    bytes += blobProps?.ContentLength ?? 0;
                }
            }
            var ext = item.IsFile ? Path.GetExtension(item.Name) : null;
            dict[item.Name] = new FileMetadata
            {
                IsFile = item.IsFile,
                Bytes = bytes,
                Extension = ext,
                Created = (item.Created ?? DateTimeOffset.UtcNow).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Changed = (item.Modified ?? DateTimeOffset.UtcNow).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
        return dict;
    }

    public async Task<bool> CreateFileAsync(string username, string filename, Stream content, CancellationToken cts = default)
    {
        filename = NormalizePath(filename);

        if (ShouldIgnoreFile(filename))
            return false;

        if (await _blob.ExistsAsync(username, filename, cts))
            return false;

        await _blob.UploadAsync(username, filename, content, cts);
        await SaveFileVersionAsync(username, filename, cts);
        return true;
    }

    public Task<bool> CreateFolderAsync(string username, string foldername)
    {
        foldername = NormalizePath(foldername);

        if (ShouldIgnoreFolder(foldername))
            return Task.FromResult(false);

        var placeholder = foldername.TrimEnd('/') + "/__folder_placeholder";

        return Task.Run(async () =>
        {
            if (await _blob.ExistsAsync(username, placeholder))
                return false;

            using var ms = new MemoryStream(Array.Empty<byte>());
            await _blob.UploadAsync(username, placeholder, ms);
            return true;
        });
    }

    public async Task SaveFileAsync(string username, string filename, Stream content, CancellationToken cts = default)
    {
        filename = NormalizePath(filename);

        if (ShouldIgnoreFile(filename))
            return;

        string? oldHash = null;
        bool fileExisted = await _blob.ExistsAsync(username, filename, cts);

        if (fileExisted)
        {
            var (oldStream, _) = await _blob.DownloadAsync(username, filename, cts);
            if (oldStream != null)
            {
                oldHash = ComputeSha256Hash(oldStream);
                await oldStream.DisposeAsync();
            }
        }

        using (var ms = new MemoryStream())
        {
            await content.CopyToAsync(ms, cts);
            ms.Position = 0;
            await _blob.UploadAsync(username, filename, ms, cts);

            ms.Position = 0;
            var newHash = ComputeSha256Hash(ms);

            if (!fileExisted || oldHash != newHash)
            {
                await SaveFileVersionAsync(username, filename, cts);
            }
        }
    }
    private async Task SaveFileVersionAsync(string username, string filename, CancellationToken cts = default)
    {
        filename = NormalizePath(filename);

        if (ShouldIgnoreFile(filename))
            return;

        var (currentStream, _) = await _blob.DownloadAsync(username, filename, cts);
        if (currentStream == null) return;

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync(cts);

            var lastVersion = await _dbContext.FileHistories
                .Where(f => f.Username == username && f.FilePath == filename)
                .OrderByDescending(f => f.Version)
                .Select(f => f.Version)
                .FirstOrDefaultAsync(cts);

            var nextVersion = lastVersion + 1;
            var historyBlobName = $"{username}/.history/{filename}/{nextVersion}.bin";

            using (currentStream)
            using (var copy = new MemoryStream())
            {
                await currentStream.CopyToAsync(copy, cts);
                copy.Position = 0;
                await _blob.UploadAsync(username, historyBlobName, copy, cts);

                var newVersion = new FileHistory
                {
                    Username = username,
                    FilePath = filename,
                    Version = nextVersion,
                    StoragePath = historyBlobName,
                    SizeBytes = copy.Length,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.FileHistories.Add(newVersion);
                await _dbContext.SaveChangesAsync(cts);
                await transaction.CommitAsync(cts);
            }
        });
    }

    public async Task<List<FileHistory>> GetFileHistoryAsync(string username, string filename, CancellationToken cts = default)
    {
        filename = NormalizePath(filename);
        return await _dbContext.FileHistories
            .Where(f => f.Username == username && f.FilePath == filename)
            .OrderByDescending(f => f.Version)
            .ToListAsync(cts);
    }

    public async Task<(Stream, string)> GetFileHistoryVersionContentAsync(string username, string filename, int version, CancellationToken cts = default)
    {
        filename = NormalizePath(filename);

        var fileVersion = await _dbContext.FileHistories
            .Where(f => f.Username == username && f.FilePath == filename && f.Version == version)
            .FirstOrDefaultAsync(cts);

        if (fileVersion == null)
            return (null!, null!);

        var blobName = fileVersion.StoragePath;
        var (stream, _) = await _blob.DownloadAsync(username, blobName.Replace($"{username}/", ""), cts);
        if (stream == null)
            return (null!, null!);

        return (stream, GetContentType(filename));
    }

    public async Task RestoreFileHistoryAsync(string username, string filename, int version, CancellationToken cts = default)
    {
        filename = NormalizePath(filename);

        var fileVersion = await _dbContext.FileHistories
            .Where(f => f.Username == username && f.FilePath == filename && f.Version == version)
            .FirstOrDefaultAsync(cts);

        if (fileVersion == null)
            throw new InvalidOperationException("File version not found.");

        var blobName = fileVersion.StoragePath;
        var (versionStream, _) = await _blob.DownloadAsync(username, blobName.Replace($"{username}/", ""), cts);
        if (versionStream == null)
            throw new InvalidOperationException("Stored file version not found.");

        using (versionStream)
        using (var ms = new MemoryStream())
        {
            await versionStream.CopyToAsync(ms, cts);
            ms.Position = 0;
            await _blob.UploadAsync(username, filename, ms, cts);
        }

        await SaveFileVersionAsync(username, filename, cts);
    }
    public async Task<List<(string File, bool Success, string? Error)>> UploadFilesAsync(
        string username,
        IEnumerable<(string FileName, Stream Content)> files,
        CancellationToken cts = default)
    {
        var results = new List<(string File, bool Success, string? Error)>();

        foreach (var (fileName, content) in files)
        {
            try
            {
                var created = await CreateFileAsync(username, fileName, content, cts);
                results.Add((fileName, created, created ? null : "File already exists"));
            }
            catch (Exception ex)
            {
                results.Add((fileName, false, ex.Message));
            }
        }

        return results;
    }

    public async Task<bool> RenameFileAsync(string username, string oldFilename, string newFilename)
    {
        oldFilename = NormalizePath(oldFilename);
        newFilename = NormalizePath(newFilename);

        if (!await _blob.ExistsAsync(username, oldFilename))
            return false;

        if (await _blob.ExistsAsync(username, newFilename))
            return false;

        await _blob.CopyAsync(username, oldFilename, newFilename);

        var histories = _dbContext.FileHistories
            .Where(f => f.Username == username && f.FilePath == oldFilename)
            .ToList();

        foreach (var history in histories)
        {
            bool duplicateExists = _dbContext.FileHistories.Any(f =>
                f.Username == username &&
                f.FilePath == newFilename &&
                f.Version == history.Version);

            if (duplicateExists)
                throw new InvalidOperationException(
                    $"Det finns redan historik för '{newFilename}' med version {history.Version}.");
        }

        foreach (var history in histories)
            history.FilePath = newFilename;

        _dbContext.SaveChanges();
        return true;
    }

    public async Task<bool> RenameFolderAsync(string username, string oldFoldername, string newFoldername)
    {
        oldFoldername = NormalizePath(oldFoldername);
        newFoldername = NormalizePath(newFoldername);

        var allBlobs = await _blob.ListAllFilesRecursiveAsync(username, oldFoldername);
        if (allBlobs.Count == 0)
            return false;

        foreach (var fullName in allBlobs)
        {
            var relative = fullName.Substring($"{username}/".Length);
            var newRelative = relative.Replace(
                oldFoldername.TrimEnd('/') + "/",
                newFoldername.TrimEnd('/') + "/");

            await _blob.CopyAsync(username, relative, newRelative);
        }

        var histories = _dbContext.FileHistories
            .Where(f => f.Username == username && f.FilePath.StartsWith(oldFoldername + "/"))
            .ToList();

        foreach (var history in histories)
        {
            history.FilePath =
                newFoldername.TrimEnd('/') +
                history.FilePath.Substring(oldFoldername.Length);
        }

        _dbContext.SaveChanges();
        return true;
    }

    public async Task<Stream?> GetFolderAsZipAsync(string username, string foldername)
    {
        foldername = NormalizePath(foldername);

        var allBlobs = await _blob.ListAllFilesRecursiveAsync(username, foldername);
        if (allBlobs.Count == 0)
            return null;

        var zipStream = new MemoryStream();

        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            var prefix = $"{username}/{foldername.TrimEnd('/')}/";

            foreach (var blobName in allBlobs)
            {
                if (!blobName.StartsWith(prefix))
                    continue;

                var entryName = blobName.Substring(prefix.Length);

                if (Path.GetFileName(entryName).Equals("__folder_placeholder", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (stream, _) = await _blob.DownloadAsync(
                    username,
                    blobName.Substring($"{username}/".Length));

                if (stream == null)
                    continue;

                using (stream)
                {
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                    using var entryStream = entry.Open();
                    await stream.CopyToAsync(entryStream);
                }
            }
        }

        zipStream.Position = 0;
        return zipStream;
    }
    public async Task<List<BinItem>> GetBinItemsAsync(string username)
    {
        return await _dbContext.BinItems
            .Where(b => b.Username == username)
            .OrderByDescending(b => b.DeletedAt)
            .ToListAsync();
    }

    public async Task<bool> DeleteFileAsync(string username, string filename)
    {
        filename = NormalizePath(filename);

        if (!await _blob.ExistsAsync(username, filename))
            return false;

        var binId = Guid.NewGuid().ToString("N");
        var binStoragePath = $".bin/{binId}/{filename}";

        await _blob.CopyAsync(username, filename, binStoragePath);

        var props = await _blob.GetPropertiesAsync(username, filename);
        long bytes = props?.ContentLength ?? 0;

        var binItem = new BinItem
        {
            Username = username,
            OriginalPath = filename,
            StoragePath = binStoragePath,
            IsFile = true,
            SizeBytes = bytes,
            DeletedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _dbContext.BinItems.Add(binItem);
        await _dbContext.SaveChangesAsync();

        await _hub.Clients.User(username)
            .SendAsync("Event", (int)EventsType.FileDeleted, filename);

        return true;
    }

    public async Task<bool> DeleteFolderAsync(string username, string foldername)
    {
        foldername = NormalizePath(foldername);

        var allBlobs = await _blob.ListAllFilesRecursiveAsync(username, foldername);
        if (allBlobs.Count == 0)
            return false;

        var binId = Guid.NewGuid().ToString("N");
        long totalBytes = 0;

        foreach (var blobName in allBlobs)
        {
            var relative = blobName.Substring($"{username}/".Length);
            var binStoragePath = $".bin/{binId}/{relative}";

            var props = await _blob.GetPropertiesAsync(username, relative);
            totalBytes += props?.ContentLength ?? 0;

            await _blob.CopyAsync(username, relative, binStoragePath);
        }

        // Restore the history records for files inside the folder
        var histories = await _dbContext.FileHistories
            .Where(f => f.Username == username && f.FilePath.StartsWith(foldername + "/"))
            .ToListAsync();

        foreach (var history in histories)
        {
            history.FilePath = $".bin/{binId}/" + history.FilePath;
        }

        if (histories.Count > 0)
            await _dbContext.SaveChangesAsync();

        var binItem = new BinItem
        {
            Username = username,
            OriginalPath = foldername,
            StoragePath = $".bin/{binId}/",
            IsFile = false,
            SizeBytes = totalBytes,
            DeletedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _dbContext.BinItems.Add(binItem);
        await _dbContext.SaveChangesAsync();

        await _hub.Clients.User(username)
            .SendAsync("Event", (int)EventsType.FolderDeleted, foldername);

        return true;
    }

    public async Task<bool> RestoreFromBinAsync(string username, long binItemId)
    {
        var binItem = await _dbContext.BinItems
            .FirstOrDefaultAsync(b => b.Id == binItemId && b.Username == username);

        if (binItem == null)
            return false;

        if (binItem.IsFile)
        {
            if (await _blob.ExistsAsync(username, binItem.OriginalPath))
                return false;

            await _blob.CopyAsync(username, binItem.StoragePath, binItem.OriginalPath);
        }
        else
        {
            var allInBin = await _blob.ListAllFilesRecursiveAsync(username, binItem.StoragePath.TrimEnd('/'));
            var prefix = binItem.StoragePath.TrimEnd('/') + "/";

            foreach (var fullName in allInBin)
            {
                var relative = fullName.Substring($"{username}/".Length);
                var originalRelative = relative.Substring(prefix.Length);

                if (await _blob.ExistsAsync(username, originalRelative))
                    continue;

                await _blob.CopyAsync(username, relative, originalRelative);
            }

            var histories = await _dbContext.FileHistories
                .Where(f => f.Username == username && f.FilePath.StartsWith(prefix))
                .ToListAsync();

            foreach (var history in histories)
            {
                history.FilePath = history.FilePath.Substring(prefix.Length);
            }

            if (histories.Count > 0)
                await _dbContext.SaveChangesAsync();
        }

        _dbContext.BinItems.Remove(binItem);
        await _dbContext.SaveChangesAsync();

        await _hub.Clients.User(username)
            .SendAsync("Event", (int)EventsType.BinRestored, binItem.OriginalPath);

        return true;
    }

    public async Task<bool> PermanentDeleteBinItemAsync(string username, long binItemId)
    {
        var binItem = await _dbContext.BinItems
            .FirstOrDefaultAsync(b => b.Id == binItemId && b.Username == username);

        if (binItem == null)
            return false;

        if (binItem.IsFile)
        {
            await _blob.DeleteAsync(username, binItem.StoragePath);
        }
        else
        {
            var allInBin = await _blob.ListAllFilesRecursiveAsync(username, binItem.StoragePath.TrimEnd('/'));
            foreach (var fullName in allInBin)
            {
                var relative = fullName.Substring($"{username}/".Length);
                await _blob.DeleteAsync(username, relative);
            }

            var prefix = binItem.StoragePath.TrimEnd('/') + "/";
            var histories = await _dbContext.FileHistories
                .Where(f => f.Username == username && f.FilePath.StartsWith(prefix))
                .ToListAsync();

            _dbContext.FileHistories.RemoveRange(histories);
        }

        _dbContext.BinItems.Remove(binItem);
        await _dbContext.SaveChangesAsync();

        await _hub.Clients.User(username)
            .SendAsync("Event", (int)EventsType.BinPermanentDeleted, binItem.OriginalPath);

        return true;
    }

    public async Task EmptyBinAsync(string username)
    {
        var items = await _dbContext.BinItems
            .Where(b => b.Username == username)
            .ToListAsync();

        foreach (var binItem in items)
        {
            if (binItem.IsFile)
            {
                await _blob.DeleteAsync(binItem.Username, binItem.StoragePath);
            }
            else
            {
                var allInBin = await _blob.ListAllFilesRecursiveAsync(binItem.Username, binItem.StoragePath.TrimEnd('/'));
                foreach (var fullName in allInBin)
                {
                    var relative = fullName.Substring($"{binItem.Username}/".Length);
                    await _blob.DeleteAsync(binItem.Username, relative);
                }

                var prefix = binItem.StoragePath.TrimEnd('/') + "/";
                var histories = await _dbContext.FileHistories
                    .Where(f => f.Username == binItem.Username && f.FilePath.StartsWith(prefix))
                    .ToListAsync();
                _dbContext.FileHistories.RemoveRange(histories);
            }

            _dbContext.BinItems.Remove(binItem);
        }

        if (items.Count > 0)
            await _dbContext.SaveChangesAsync();
    }

    public async Task CleanExpiredBinItemsAsync()
    {
        var expired = await _dbContext.BinItems
            .Where(b => b.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var binItem in expired)
        {
            if (binItem.IsFile)
            {
                await _blob.DeleteAsync(binItem.Username, binItem.StoragePath);
            }
            else
            {
                var allInBin = await _blob.ListAllFilesRecursiveAsync(binItem.Username, binItem.StoragePath.TrimEnd('/'));
                foreach (var fullName in allInBin)
                {
                    var relative = fullName.Substring($"{binItem.Username}/".Length);
                    await _blob.DeleteAsync(binItem.Username, relative);
                }

                var prefix = binItem.StoragePath.TrimEnd('/') + "/";
                var histories = await _dbContext.FileHistories
                    .Where(f => f.Username == binItem.Username && f.FilePath.StartsWith(prefix))
                    .ToListAsync();

                _dbContext.FileHistories.RemoveRange(histories);
            }

            _dbContext.BinItems.Remove(binItem);
        }

        if (expired.Count > 0)
            await _dbContext.SaveChangesAsync();
    }

    public async Task<ShareLink> CreateShareLinkAsync(string username, string filePath)
    {
        filePath = NormalizePath(filePath);

        var token = Guid.NewGuid().ToString("N");
        var shareLink = new ShareLink
        {
            Token = token,
            Username = username,
            FilePath = filePath,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ShareLinks.Add(shareLink);
        await _dbContext.SaveChangesAsync();

        return shareLink;
    }

    public async Task<ShareLink?> GetShareLinkByTokenAsync(string token)
    {
        return await _dbContext.ShareLinks
            .FirstOrDefaultAsync(s => s.Token == token);
    }

    public async Task<bool> FileExistsAsync(string username, string filename)
    {
        filename = NormalizePath(filename);
        return await _blob.ExistsAsync(username, filename);
    }

    public async Task<bool> FolderExistsAsync(string username, string foldername)
    {
        foldername = NormalizePath(foldername);
        var list = await _blob.ListFolderAsync(username, foldername);
        return list.Count > 0;
    }

    private string GetContentType(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();

        if (ext == ".ts" || ext == ".tsx")
            return "text/plain";

        if (ext == ".js" || ext == ".jsx")
            return "application/javascript";

        if (_contentTypeProvider.TryGetContentType(filename, out var contentType))
            return contentType;

        string[] textExts = new[]
        {
            ".txt", ".md", ".markdown", ".csv", ".log", ".json", ".xml", ".yml", ".yaml",
            ".ini", ".conf", ".config", ".env", ".bat", ".sh", ".ps1", ".cmd",
            ".c", ".cpp", ".h", ".hpp", ".cs", ".vb", ".java", ".py", ".rb", ".php",
            ".go", ".rs", ".swift", ".kt", ".kts", ".scala", ".clj", ".cljs",
            ".groovy", ".dart", ".sql", ".scss", ".sass", ".less", ".css",
            ".tex", ".r", ".m", ".pl", ".lua", ".fs", ".fsx", ".erl", ".ex", ".exs",
            ".f90", ".f", ".f77", ".f95", ".asm", ".s", ".mak", ".cmake",
            ".dockerfile", ".gitignore", ".gitattributes", ".editorconfig",
            ".properties", ".toml"
        };

        if (textExts.Contains(ext))
            return "text/plain";

        return "application/octet-stream";
    }
}
