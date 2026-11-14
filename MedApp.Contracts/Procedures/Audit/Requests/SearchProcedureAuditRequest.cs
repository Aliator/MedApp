namespace MedApp.Contracts.Procedures.Audit.Requests;

/// <summary>Query string for <c>GET api/procedures/audit/search</c> (admins only). Every filter is optional.</summary>
public sealed class SearchProcedureAuditRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// <summary>Oldest first; newest first by default.</summary>
    public bool Ascending { get; set; }

    public Guid? ProcedureId { get; set; }

    public Guid? PatientId { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>Created, DetailsUpdated, StatusChanged, Assigned, Unassigned, StageStarted, StageNotesUpdated, StageFinished, StageUnlocked or Deleted.</summary>
    public string? Action { get; set; }

    /// <summary>Matches the procedure title, stage name or username, ignoring case.</summary>
    public string? Search { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }
}
