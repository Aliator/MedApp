namespace MedApp.Contracts.Pathways.NoteDeletionRequests.Requests;

/// <summary>Asks an admin to delete a note on a pathway: every admin is told, and one of them decides.</summary>
public sealed class RequestNoteDeletionRequest
{
    /// <summary>Why: WrongPatient, WrittenInError, Duplicate or Other.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>More about why (at most 500 characters); required when the reason is Other.</summary>
    public string? Detail { get; set; }
}
