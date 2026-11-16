using MedApp.Application.Attachments.Common;

namespace MedApp.Infrastructure.Attachments;

/// <summary>
/// The file store when none is set up (no <c>ConnectionStrings:Attachments</c>): handlers check
/// <see cref="IsConfigured"/> and refuse uploads and downloads (503), so the rest of the app runs without storage.
/// </summary>
public sealed class UnconfiguredFileStore : IFileStore
{
    public bool IsConfigured => false;

    public Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct) =>
        throw new FileStorageUnavailableException();

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) =>
        throw new FileStorageUnavailableException();

    public Task DeleteAsync(string key, CancellationToken ct) =>
        throw new FileStorageUnavailableException();
}
