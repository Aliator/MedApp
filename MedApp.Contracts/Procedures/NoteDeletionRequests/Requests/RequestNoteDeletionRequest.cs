namespace MedApp.Contracts.Procedures.NoteDeletionRequests.Requests;

/// <summary>Asks an admin to delete a note on a procedure: every admin is told, and one of them decides.</summary>
public sealed class RequestNoteDeletionRequest
{
    /// <summary>Why it should be deleted (required, at most 500 characters).</summary>
    public string Reason { get; set; } = string.Empty;
}
