using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HOMSys.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace HOMSys.Infrastructure.Storage;

/// <summary>
/// IFileStorage on Azure Blob Storage (account sthomsyssea, rg-homsys-sea). The connection string is
/// a secret: App Service setting Storage__ConnectionString / local appsettings.Development.json —
/// never in the repo. Containers are created private on first use.
/// </summary>
public class BlobFileStorage(IConfiguration config) : IFileStorage
{
    private readonly string? connectionString = config["Storage:ConnectionString"];
    /// <summary>Test setups (a copy of live) use e.g. "-dev" so they never overwrite live files.</summary>
    private readonly string suffix = config["Storage:ContainerSuffix"] ?? "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(connectionString)
        && !connectionString.StartsWith("SET_VIA", StringComparison.OrdinalIgnoreCase);

    // One client for the app's lifetime (registered as a singleton); thread-safe per the SDK.
    private BlobServiceClient? service;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> ensured = new();

    /// <summary>The container client. Only writes make sure it exists (once per container) — reads don't pay that round trip.</summary>
    private async Task<BlobContainerClient> ContainerAsync(string container, bool forWrite = false)
    {
        if (!IsConfigured) throw new InvalidOperationException("File storage is not configured (Storage:ConnectionString).");
        service ??= new BlobServiceClient(connectionString);
        var client = service.GetBlobContainerClient(container + suffix);
        if (forWrite && ensured.TryAdd(client.Name, true))
            await client.CreateIfNotExistsAsync(PublicAccessType.None);
        return client;
    }

    public async Task SaveAsync(string container, string name, byte[] data, string contentType)
    {
        var blob = (await ContainerAsync(container, forWrite: true)).GetBlobClient(name);
        await blob.UploadAsync(new BinaryData(data), new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        });
    }

    public async Task<(byte[] Data, string ContentType)?> ReadAsync(string container, string name)
    {
        try
        {
            var result = await (await ContainerAsync(container)).GetBlobClient(name).DownloadContentAsync();
            return (result.Value.Content.ToArray(), result.Value.Details.ContentType ?? "application/octet-stream");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string container, string name) =>
        await (await ContainerAsync(container)).GetBlobClient(name).DeleteIfExistsAsync();
}
