namespace MedApp.Application.Attachments.Common;

/// <summary>
/// Somewhere to hold an upload while it's checked, so it can be read more than once (its type, the malware scan, the
/// thumbnail) without holding up to 20 MB in memory: a temporary file, deleted when the stream is disposed.
/// </summary>
public interface IUploadStaging
{
    /// <summary>A new, empty, readable, writable and seekable stream.</summary>
    Stream Create();
}
