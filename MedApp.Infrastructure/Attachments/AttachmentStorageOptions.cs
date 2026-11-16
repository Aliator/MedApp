using Azure.Core;
using Azure.Storage.Blobs;

namespace MedApp.Infrastructure.Attachments;

/// <summary>
/// Where attached files are stored. The connection string is <c>ConnectionStrings:Attachments</c>: an Azure Storage
/// connection string (Azurite's in development, from the AppHost, and on kind; the storage account's in Azure, from
/// Key Vault). Without one, file storage isn't set up: uploads and downloads are refused (503), and the rest of the
/// app works as before.
/// </summary>
public sealed class AttachmentStorageOptions
{
    public const string Section = "Attachments";

    /// <summary>The name of the connection string (under <c>ConnectionStrings</c>).</summary>
    public const string ConnectionStringName = "Attachments";

    /// <summary>The blob container the files go in; the API creates it if it's missing.</summary>
    public string Container { get; set; } = "attachments";

    /// <summary>
    /// The storage client's retries and timeouts: at most 2 retries, each try given 30 seconds, so a storage account
    /// that stops answering fails an upload in about a minute and a half, as a 503, rather than after the SDK's
    /// defaults (3 retries of 100 seconds each).
    /// </summary>
    public static BlobClientOptions ClientOptions() => new()
    {
        Retry =
        {
            Mode = RetryMode.Exponential,
            MaxRetries = 2,
            Delay = TimeSpan.FromMilliseconds(500),
            MaxDelay = TimeSpan.FromSeconds(5),
            NetworkTimeout = TimeSpan.FromSeconds(30),
        },
    };

    /// <summary>
    /// The connection string without a <c>ContainerName=</c> part, which Aspire adds to a blob container's connection
    /// string and the storage client doesn't accept. The container comes from <see cref="Container"/>.
    /// </summary>
    public static string WithoutContainerName(string connectionString) =>
        string.Join(';', connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.TrimStart().StartsWith("ContainerName=", StringComparison.OrdinalIgnoreCase)));
}
