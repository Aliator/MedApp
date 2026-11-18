using MedApp.Contracts.Pathways.PatientPathways.Responses;

namespace MedApp.Contracts.Pathways.NoteDeletionRequests.Responses;

/// <summary>
/// A request to delete a note on a pathway, as an admin decides it: the note, the patient (as the patient banner shows
/// them), the pathway, who asked, when and why, and, once decided, the outcome.
/// </summary>
public sealed class NoteDeletionRequestResponse
{
    public Guid Id { get; set; }

    /// <summary>Pending, Approved (the note was deleted), Declined (it was kept) or Closed (its pathway was deleted, or the record anonymised).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Why the member of staff asked: WrongPatient, WrittenInError, Duplicate or Other.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>More about why, if they gave any.</summary>
    public string? Detail { get; set; }

    /// <summary>Who asked, by the username recorded at the time.</summary>
    public string RequestedByUsername { get; set; } = string.Empty;

    public DateTime RequestedAtUtc { get; set; }

    /// <summary>The admin who decided, by the username recorded at the time; null while it's pending.</summary>
    public string? DecidedByUsername { get; set; }

    public DateTime? DecidedAtUtc { get; set; }

    public Guid PathwayId { get; set; }

    /// <summary>The pathway's title; null once the pathway has been deleted.</summary>
    public string? PathwayTitle { get; set; }

    /// <summary>Who the note is about, as the patient banner shows them; null once the patient has been deleted.</summary>
    public PathwayPatientResponse? Patient { get; set; }

    /// <summary>The note; null once its pathway has been deleted (and the note with it).</summary>
    public NoteDeletionRequestNoteResponse? Note { get; set; }

    /// <summary>Whether it can be decided now: it's pending and the patient's record can change.</summary>
    public bool CanDecide { get; set; }

    /// <summary>Why a pending request can't be decided now (the record is restricted or anonymised), or null.</summary>
    public string? WhyCantDecide { get; set; }
}

/// <summary>The note a deletion request is about.</summary>
public sealed class NoteDeletionRequestNoteResponse
{
    public Guid Id { get; set; }

    /// <summary>Its text now, as Markdown (its last text, if it has been deleted).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Its author's username, "Deleted user" once their account is gone, or "Unknown" when not recorded.</summary>
    public string AuthorUsername { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>When it was last edited; null if it never has been. After the request was made, the reason may be about other words.</summary>
    public DateTime? LastEditedAtUtc { get; set; }

    /// <summary>Its version now: send it back as <c>expectedVersion</c> to delete it.</summary>
    public int Version { get; set; }

    /// <summary>When an admin deleted it; null while it isn't deleted.</summary>
    public DateTime? DeletedAtUtc { get; set; }

    /// <summary>The admin who deleted it ("Deleted user" once their account is gone); null while it isn't deleted.</summary>
    public string? DeletedByUsername { get; set; }

    /// <summary>Why it was deleted (WrongPatient, WrittenInError, Duplicate or Other); null while it isn't deleted.</summary>
    public string? DeletionReason { get; set; }

    /// <summary>More about why it was deleted, if the admin gave any.</summary>
    public string? DeletionDetail { get; set; }
}
