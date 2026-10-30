namespace HOMSys.Application.Interfaces;

/// <summary>File storage for collection receipt soft copies: Azure Blob in production, a local folder in dev.</summary>
public interface IAttachmentStore
{
    Task SaveAsync(string path, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Null when the file is gone.</summary>
    Task<Stream?> OpenAsync(string path, CancellationToken ct = default);

    Task DeleteAsync(string path, CancellationToken ct = default);
}
