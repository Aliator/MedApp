using Azure.Storage.Blobs;
using MedApp.Infrastructure.Attachments;
using Microsoft.Extensions.Configuration;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Whether the demo's files can be stored: the report the GP practice sent (the story uploads it through the app's own
/// command) needs the attachment store (the AppHost's Azurite). Without one, the files the app's commands make are
/// dropped and their records stay, and the summary says so.
/// </summary>
public static class DemoFiles
{
    /// <summary>Whether files can be stored: a local attachment store is configured and answers. Null if so; else why not.</summary>
    public static async Task<string?> WhyNotAvailableAsync(IConfiguration configuration, CancellationToken ct)
    {
        var connection = configuration.GetConnectionString(AttachmentStorageOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connection))
            return "No attachment store is configured (ConnectionStrings:Attachments), so no files were added.";

        try
        {
            var client = new BlobServiceClient(AttachmentStorageOptions.WithoutContainerName(connection),
                new BlobClientOptions { Retry = { MaxRetries = 0, NetworkTimeout = TimeSpan.FromSeconds(3) } });
            if (!client.Uri.IsLoopback)
                return $"The attachment store isn't local ({client.Uri.Host}), so no files were added.";

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await client.GetPropertiesAsync(timeout.Token);
            return null;
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            return $"The attachment store didn't answer ({e.GetType().Name}: {e.Message.Split('\n')[0]}); is the AppHost's Azurite running? No files were added.";
        }
    }
}
