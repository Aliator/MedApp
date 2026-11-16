namespace MedApp.Application.Attachments.Common;

/// <summary>
/// Where attached files' bytes are kept (Azure Blob Storage, or Azurite in development and on kind), by key
/// (<see cref="AttachmentStorageKeys"/>). Their metadata is in SQL (<c>Attachment</c>).
/// </summary>
public interface IFileStore
{
    /// <summary>Whether storage is set up. Without it, uploads and downloads are refused (503) and the rest still works.</summary>
    bool IsConfigured { get; }

    /// <summary>Stores <paramref name="content"/> (read from where it is to its end) under a new key; never overwrites.</summary>
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct);

    /// <summary>The stored bytes, streamed; null if nothing is stored under the key.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);

    /// <summary>Deletes what's stored under the key, if anything.</summary>
    Task DeleteAsync(string key, CancellationToken ct);
}
