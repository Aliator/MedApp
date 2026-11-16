namespace MedApp.Contracts.Attachments.Responses;

/// <summary>Files attached to something, newest first: at most the newest few hundred, and how many there are in all.</summary>
public sealed class AttachmentListResponse
{
    public List<AttachmentResponse> Items { get; set; } = [];

    /// <summary>How many there are, which may be more than <see cref="Items"/> holds.</summary>
    public int TotalCount { get; set; }

    /// <summary>The largest file the API accepts, in bytes.</summary>
    public long MaxFileBytes { get; set; }

    /// <summary>The kinds of file the API accepts, by extension (e.g. .pdf), for a file picker's hint.</summary>
    public List<string> AllowedExtensions { get; set; } = [];
}
