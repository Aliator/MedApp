namespace MedApp.Contracts.Procedures.NoteDeletionRequests.Requests;

/// <summary>An admin's decision on a request to delete a note: delete it, or keep it.</summary>
public sealed class DecideNoteDeletionRequestRequest
{
    /// <summary>True to delete the note; false to keep it (the request is declined).</summary>
    public bool DeleteNote { get; set; }

    /// <summary>
    /// When deleting: the reason recorded with the deletion (at most 500 characters). Leave it out to use the
    /// requester's reason. Ignored when keeping the note.
    /// </summary>
    public string? Reason { get; set; }
}
