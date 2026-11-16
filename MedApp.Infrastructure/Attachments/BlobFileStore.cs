using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MedApp.Application.Attachments.Common;

namespace MedApp.Infrastructure.Attachments;

/// <summary>
/// Attached files in Azure Blob Storage (or Azurite), one blob per key, in one private container. Only the API reads
/// it: browsers never get a link to a blob, so every download goes through the API's checks and headers. The
/// container is created the first time it's needed. Any failure but a missing blob, once the client's own retries are
/// spent (<see cref="AttachmentStorageOptions.ClientOptions"/>), is a <see cref="StorageUnavailableException"/> (503).
/// </summary>
public sealed class BlobFileStore(BlobContainerClient container) : IFileStore
{
    private readonly SemaphoreSlim _creating = new(1, 1);
    private volatile bool _created;

    public bool IsConfigured => true;

    public Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct) =>
        Guard(async () =>
        {
            await EnsureContainerAsync(ct);
            await container.GetBlobClient(key).UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                // Never replace a blob: keys come from new IDs, so one already there is a mistake.
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
            }, ct);
            return true;
        }, ct);

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) =>
        Guard<Stream?>(async () =>
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
        }, ct);

    public Task DeleteAsync(string key, CancellationToken ct) =>
        Guard(async () => (await container.GetBlobClient(key).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct)).Value, ct);

    /// <summary>
    /// Runs a storage call, turning its failures into a <see cref="StorageUnavailableException"/>: an error answer, a
    /// connection that failed, or the client's network timeout. Cancellation by the caller stays a cancellation.
    /// </summary>
    public static async Task<T> Guard<T>(Func<Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (e is RequestFailedException or AggregateException or OperationCanceledException or IOException)
        {
            throw new StorageUnavailableException(e);
        }
    }

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
