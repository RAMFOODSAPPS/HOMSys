namespace HOMSys.Application.Interfaces;

/// <summary>
/// File storage (Azure Blob Storage — Infrastructure BlobFileStorage, "Storage:ConnectionString").
/// Containers are private; files are served through the API. Not configured = every call fails
/// with a clear message instead of touching storage.
/// </summary>
public interface IFileStorage
{
    bool IsConfigured { get; }
    Task SaveAsync(string container, string name, byte[] data, string contentType);
    /// <summary>The file, or null when it doesn't exist.</summary>
    Task<(byte[] Data, string ContentType)?> ReadAsync(string container, string name);
    Task DeleteAsync(string container, string name);
}
