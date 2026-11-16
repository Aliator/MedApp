using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MedApp.Application.Attachments.Common;

namespace MedApp.Infrastructure.Attachments;

/// <summary>
/// Attached files in Azure Blob Storage (or Azurite), one blob per key, in one private container. Only the API reads
/// it: browsers never get a link to a blob, so every download goes through the API's checks and headers. The
/// container is created the first time it's needed.
/// </summary>
public sealed class BlobFileStore(BlobContainerClient container) : IFileStore
{
    private readonly SemaphoreSlim _creating = new(1, 1);
    private volatile bool _created;

    public bool IsConfigured => true;

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        await EnsureContainerAsync(ct);
        await container.GetBlobClient(key).UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            // Never replace a blob: keys come from new IDs, so one already there is a mistake.
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
        }, ct);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        try
        {
            var download = await container.GetBlobClient(key).DownloadStreamingAsync(cancellationToken: ct);
            return download.Value.Content;
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct) =>
        await container.GetBlobClient(key).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);

    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_created)
            return;

        await _creating.WaitAsync(ct);
        try
        {
            if (!_created)
            {
                await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
                _created = true;
            }
        }
        finally
        {
            _creating.Release();
        }
    }
}
