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
    /// The connection string without a <c>ContainerName=</c> part, which Aspire adds to a blob container's connection
    /// string and the storage client doesn't accept. The container comes from <see cref="Container"/>.
    /// </summary>
    public static string WithoutContainerName(string connectionString) =>
        string.Join(';', connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.TrimStart().StartsWith("ContainerName=", StringComparison.OrdinalIgnoreCase)));
}
