namespace MedApp.Contracts.Pathways.PatientPathways.Requests;

/// <summary>Deletes a note on a pathway (admins): it's hidden from the pathway page, but kept, with the reason.</summary>
public sealed class DeletePathwayNoteRequest
{
    /// <summary>Why: WrongPatient, WrittenInError, Duplicate or Other.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>More about why (at most 500 characters); required when the reason is Other.</summary>
    public string? Detail { get; set; }

    /// <summary>The note's version the admin read; if it has been edited since, the deletion is refused (409).</summary>
    public int ExpectedVersion { get; set; }
}
