namespace MedApp.Application.Attachments.Common;

/// <summary>
/// A file (or thumbnail) to send: the name to save it as, its type, its length if known, and its bytes, streamed from
/// the file store. Whoever sends it disposes <paramref name="Content"/>.
/// </summary>
public sealed record AttachmentDownload(string FileName, string ContentType, long? Length, Stream Content);
