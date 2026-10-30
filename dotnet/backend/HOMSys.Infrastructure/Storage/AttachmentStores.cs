using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HOMSys.Application.Interfaces;

namespace HOMSys.Infrastructure.Storage;

/// <summary>Private Azure Blob container; files are only ever served through the authorized API.</summary>
public class BlobAttachmentStore(string connectionString, string containerName) : IAttachmentStore
{
    private readonly BlobContainerClient container = new(connectionString, containerName);
    private bool ensured;

    private async Task<BlobContainerClient> ContainerAsync(CancellationToken ct)
    {
        if (!ensured)
        {
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            ensured = true;
        }
        return container;
    }

    public async Task SaveAsync(string path, Stream content, string contentType, CancellationToken ct = default)
    {
        var c = await ContainerAsync(ct);
        await c.GetBlobClient(path).UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, ct);
    }

    public async Task<Stream?> OpenAsync(string path, CancellationToken ct = default)
    {
        var blob = (await ContainerAsync(ct)).GetBlobClient(path);
        return await blob.ExistsAsync(ct) ? await blob.OpenReadAsync(cancellationToken: ct) : null;
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default) =>
        await (await ContainerAsync(ct)).GetBlobClient(path).DeleteIfExistsAsync(cancellationToken: ct);
}

/// <summary>Dev / local-test store under a plain folder (never used when a blob connection string is set).</summary>
public class LocalAttachmentStore(string root) : IAttachmentStore
{
    private string Full(string path)
    {
        var full = Path.GetFullPath(Path.Combine(root, path));
        if (!full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Attachment path escapes the store root.");
        return full;
    }

    public async Task SaveAsync(string path, Stream content, string contentType, CancellationToken ct = default)
    {
        var full = Full(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream?> OpenAsync(string path, CancellationToken ct = default)
    {
        var full = Full(path);
        return Task.FromResult<Stream?>(File.Exists(full) ? File.OpenRead(full) : null);
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var full = Full(path);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }
}
