namespace MedApp.Application.Attachments.Common;

/// <summary>Where an attachment's bytes and thumbnail are kept in the file store, from its ID (nothing else).</summary>
public static class AttachmentStorageKeys
{
    public static string File(Guid attachmentId) => $"files/{attachmentId:N}";

    public static string Thumbnail(Guid attachmentId) => $"thumbnails/{attachmentId:N}";
}
