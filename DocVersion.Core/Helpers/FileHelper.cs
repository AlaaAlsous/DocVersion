using DocVersion.Core.Models;

namespace DocVersion.Core.Helpers;

public static class FileHelper
{
    public static Dictionary<string, FileMetadata> GetFolderContent(string folderPath)
    {
        var result = new Dictionary<string, FileMetadata>();

        foreach (var file in Directory.GetFiles(folderPath))
        {
            var fileInfo = new FileInfo(file);
            result[fileInfo.Name] = new FileMetadata
            {
                Created = fileInfo.CreationTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                Changed = fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                IsFile = true,
                Bytes = fileInfo.Length,
                Extension = fileInfo.Extension
            };
        }

        foreach (var folder in Directory.GetDirectories(folderPath))
        {
            var folderInfo = new DirectoryInfo(folder);
            result[folderInfo.Name] = new FileMetadata
            {
                Created = folderInfo.CreationTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                Changed = folderInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                IsFile = false,
                Bytes = CalculateDirectorySize(folder),
                Extension = null,
                Content = GetFolderContent(folder)
            };
        }

        return result;
    }

    public static long CalculateDirectorySize(string folderPath)
    {
        long totalBytes = 0;
        foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
        {
            try { totalBytes += new FileInfo(file).Length; }
            catch { }
        }
        return totalBytes;
    }

    public static void PrepareDirectoryForDelete(string folder)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(path, FileAttributes.Normal); }
            catch { }
        }
        try { File.SetAttributes(folder, FileAttributes.Normal); }
        catch { }
    }
}
