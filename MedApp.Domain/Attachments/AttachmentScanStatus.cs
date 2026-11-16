namespace MedApp.Domain.Attachments;

/// <summary>What the malware scan said about a file. Stored as a number, so values never change.</summary>
public enum AttachmentScanStatus
{
    /// <summary>No scanner ran (none is set up yet): the file hasn't been checked for malware.</summary>
    NotScanned = 0,

    /// <summary>A scanner checked it and found nothing.</summary>
    Clean = 1,

    /// <summary>A scanner found malware. Such files are refused, so none is stored with this status yet.</summary>
    Infected = 2
}
