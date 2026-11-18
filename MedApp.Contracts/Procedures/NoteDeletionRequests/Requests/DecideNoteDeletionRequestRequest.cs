namespace MedApp.Contracts.Procedures.NoteDeletionRequests.Requests;

/// <summary>An admin's decision on a request to delete a note: delete it, or keep it.</summary>
public sealed class DecideNoteDeletionRequestRequest
{
    /// <summary>True to delete the note; false to keep it (the request is declined).</summary>
    public bool DeleteNote { get; set; }

    /// <summary>
    /// When deleting: the note's version the admin read (required); if it has been edited since, the decision is refused
    /// (409). Ignored when keeping the note.
    /// </summary>
    public int? ExpectedVersion { get; set; }

    /// <summary>When deleting: the reason recorded (WrongPatient, WrittenInError, Duplicate or Other). Left out, the requester's.</summary>
    public string? Reason { get; set; }

    /// <summary>
    /// When deleting: more about why (at most 500 characters). Left out with the reason, the requester's; required when
    /// the reason is Other.
    /// </summary>
    public string? Detail { get; set; }
}
