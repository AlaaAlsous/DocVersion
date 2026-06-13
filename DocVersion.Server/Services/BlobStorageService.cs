using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace DocVersion.Server.Services
{
    public class BlobStorageService
    {
        private readonly BlobContainerClient _container;

        public BlobStorageService(IConfiguration config)
        {
            string? keyVaultUrl = config["KeyVaultUrl"];
            string? connFromKeyVault = null;

            if (!string.IsNullOrEmpty(keyVaultUrl))
            {
                try
                {
                    var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
                    var secret = client.GetSecret("AzureBlob-ConnectionString");
                    connFromKeyVault = secret.Value.Value;
                }
                catch { }
            }

            string? connFromEnv = Environment.GetEnvironmentVariable("AzureBlob__ConnectionString");

            string? connFromConfig = config["AzureBlob:ConnectionString"];

            string? connectionString =
                FirstNonEmpty(connFromKeyVault, connFromEnv, connFromConfig)
                ?? throw new InvalidOperationException("AzureBlob ConnectionString missing.");

            string? containerName =
                Environment.GetEnvironmentVariable("AzureBlob__ContainerName")
                ?? config["AzureBlob:ContainerName"]
                ?? throw new InvalidOperationException("AzureBlob ContainerName missing.");

            _container = new BlobContainerClient(connectionString, containerName);
            _container.CreateIfNotExists();
        }
        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var v in values)
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            return null;
        }

        private static string NormalizeBlobName(string username, string? path)
        {
            path ??= string.Empty;

            path = path.Replace("\\", "/").TrimStart('/');

            if (path.StartsWith(username + "/", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(username.Length + 1);

            return $"{username}/{path}";
        }


        public async Task UploadAsync(string username, string path, System.IO.Stream content, CancellationToken ct = default)
        {
            var blobName = NormalizeBlobName(username, path);
            var blob = _container.GetBlobClient(blobName);
            await blob.UploadAsync(content, overwrite: true, cancellationToken: ct);
        }

        public async Task<(System.IO.Stream? Stream, BlobDownloadDetails? Details)> DownloadAsync(
            string username,
            string path,
            CancellationToken ct = default)
        {
            var blobName = NormalizeBlobName(username, path);
            var blob = _container.GetBlobClient(blobName);

            var exists = await blob.ExistsAsync(ct);
            if (!exists.Value)
                return (null, null);

            var resp = await blob.DownloadAsync(ct);
            return (resp.Value.Content, resp.Value.Details);
        }

        public async Task<bool> ExistsAsync(string username, string path, CancellationToken ct = default)
        {
            var blobName = NormalizeBlobName(username, path);
            var blob = _container.GetBlobClient(blobName);
            var exists = await blob.ExistsAsync(ct);
            return exists.Value;
        }

        public async Task DeleteAsync(string username, string path, CancellationToken ct = default)
        {
            var blobName = NormalizeBlobName(username, path);
            var blob = _container.GetBlobClient(blobName);
            await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
        }

        public async Task<List<(string Name, bool IsFile, long Bytes, DateTimeOffset? Created, DateTimeOffset? Modified)>>
            ListFolderAsync(string username, string? folder, CancellationToken ct = default)
        {
            var prefix = string.IsNullOrWhiteSpace(folder)
                ? $"{username}/"
                : $"{username}/{folder.Replace("\\", "/").TrimEnd('/')}/";

            var results = new Dictionary<string, (bool IsFile, long Bytes, DateTimeOffset? Created, DateTimeOffset? Modified)>();

            await foreach (var item in _container.GetBlobsAsync(
                               BlobTraits.None,
                               BlobStates.None,
                               prefix,
                               ct))
            {
                var relative = item.Name.Substring(prefix.Length);

                if (string.IsNullOrWhiteSpace(relative))
                    continue;

                if (relative.StartsWith(".history", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (relative.StartsWith(".bin", StringComparison.OrdinalIgnoreCase))
                    continue;

                var slashIndex = relative.IndexOf('/');
                if (slashIndex == -1)
                {
                    results[relative] = (
                        true,
                        item.Properties.ContentLength ?? 0,
                        item.Properties.CreatedOn,
                        item.Properties.LastModified
                    );
                }
                else
                {
                    var folderName = relative[..slashIndex];

                    if (folderName.Equals(".history", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (folderName.Equals(".bin", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (string.IsNullOrWhiteSpace(folderName))
                        continue;

                    if (folderName.Equals(username, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!results.ContainsKey(folderName))
                    {
                        results[folderName] = (false, 0, null, null);
                    }
                }
            }

            return results
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .Select(kvp => (
                    kvp.Key,
                    kvp.Value.IsFile,
                    kvp.Value.Bytes,
                    kvp.Value.Created,
                    kvp.Value.Modified))
                .ToList();
        }

        public async Task<List<string>> ListAllFilesRecursiveAsync(string username, string folder, CancellationToken ct = default)
        {
            var prefix = string.IsNullOrWhiteSpace(folder)
                ? $"{username}/"
                : $"{username}/{folder.Replace("\\", "/").TrimEnd('/')}/";

            var list = new List<string>();

            await foreach (var item in _container.GetBlobsAsync(
                               BlobTraits.None,
                               BlobStates.None,
                               prefix,
                               ct))
            {
                list.Add(item.Name);
            }

            return list;
        }

        public async Task<BlobProperties?> GetPropertiesAsync(string username, string path, CancellationToken ct = default)
        {
            var blobName = NormalizeBlobName(username, path);
            var blob = _container.GetBlobClient(blobName);

            try
            {
                var props = await blob.GetPropertiesAsync(cancellationToken: ct);
                return props.Value;
            }
            catch
            {
                return null;
            }
        }

        public async Task CopyAsync(string username, string sourcePath, string destPath, CancellationToken ct = default)
        {
            var sourceName = NormalizeBlobName(username, sourcePath);
            var destName = NormalizeBlobName(username, destPath);

            var source = _container.GetBlobClient(sourceName);
            var dest = _container.GetBlobClient(destName);

            var exists = await source.ExistsAsync(ct);
            if (!exists.Value)
                throw new InvalidOperationException("Source blob does not exist.");

            var copyOperation = await dest.StartCopyFromUriAsync(source.Uri, cancellationToken: ct);

            while (true)
            {
                var props = await dest.GetPropertiesAsync(cancellationToken: ct);
                if (props.Value.CopyStatus != CopyStatus.Pending)
                    break;

                await Task.Delay(200, ct);
            }

            await source.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
        }
    }
}
