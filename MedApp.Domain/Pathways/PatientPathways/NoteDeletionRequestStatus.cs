namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>Where a request to delete a pathway's note stands. Stored as a number, so values never change.</summary>
public enum NoteDeletionRequestStatus
{
    /// <summary>Waiting for an admin to decide.</summary>
    Pending = 0,

    /// <summary>An admin deleted the note (from the request, or directly while it was waiting).</summary>
    Approved = 1,

    /// <summary>An admin kept the note.</summary>
    Declined = 2,

    /// <summary>Closed without a decision: the note's pathway was deleted, or the patient's record anonymised.</summary>
    Closed = 3
}
