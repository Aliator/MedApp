namespace MedApp.Application.Attachments.Common;

/// <summary>The limits on attached files.</summary>
public static class AttachmentLimits
{
    /// <summary>The largest file that can be attached: 20 MB (20 MiB). The upload endpoint's request size limit too.</summary>
    public const long MaxFileBytes = 20L * 1024 * 1024;

    /// <summary>How the limit is said to people.</summary>
    public const string MaxFileSizeText = "20 MB";

    /// <summary>The longest file name kept, in characters (with its extension).</summary>
    public const int MaxFileNameLength = 255;

    /// <summary>The most files one list returns (the newest); the response says how many there are in all.</summary>
    public const int MaxListed = 200;
}
