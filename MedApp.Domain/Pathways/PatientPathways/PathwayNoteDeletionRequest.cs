namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>
/// A member of staff's request that an admin delete a note on a pathway, with their reason. Every admin is told; one
/// of them deletes the note (<see cref="NoteDeletionRequestStatus.Approved"/>) or keeps it
/// (<see cref="NoteDeletionRequestStatus.Declined"/>), and the requester is told which. A note has at most one pending
/// request at a time (<see cref="PatientPathwayNote.PendingDeletionRequestId"/>).
/// <para>
/// Like the audit log, it has no foreign keys and copies in its patient and pathway, so it outlives a deleted
/// pathway, and a patient's export and anonymisation find it by the patient.
/// </para>
/// </summary>
public sealed class PathwayNoteDeletionRequest
{
    public Guid Id { get; init; }

    public Guid NoteId { get; init; }

    public Guid PatientPathwayId { get; init; }

    public Guid PatientId { get; init; }

    public Guid RequestedByUserId { get; init; }

    /// <summary>The requester's username at the time, kept even if the account is later deleted.</summary>
    public string RequestedByUsername { get; init; } = string.Empty;

    /// <summary>Why they asked: a fixed choice.</summary>
    public NoteDeletionReason Reason { get; init; }

    /// <summary>More about why (free text, at most <see cref="PatientPathwayNote.MaxDeletionDetailLength"/> characters), if any.</summary>
    public string? Detail { get; set; }

    public DateTime RequestedAtUtc { get; init; }

    public NoteDeletionRequestStatus Status { get; set; } = NoteDeletionRequestStatus.Pending;

    /// <summary>The admin who decided (or whose change closed it); null while it's pending.</summary>
    public Guid? DecidedByUserId { get; set; }

    /// <summary>Their username at the time.</summary>
    public string? DecidedByUsername { get; set; }

    public DateTime? DecidedAtUtc { get; set; }

    /// <summary>A concurrency token: of two admins deciding at once, the later is refused (409) rather than overwriting.</summary>
    public byte[] RowVersion { get; set; } = [];

    public bool IsPending => Status == NoteDeletionRequestStatus.Pending;

    /// <summary>Closes it with an outcome, by whom and when.</summary>
    public void Close(NoteDeletionRequestStatus outcome, Guid? byUserId, string? byUsername, DateTime atUtc)
    {
        Status = outcome;
        DecidedByUserId = byUserId;
        DecidedByUsername = byUsername;
        DecidedAtUtc = atUtc;
    }
}
