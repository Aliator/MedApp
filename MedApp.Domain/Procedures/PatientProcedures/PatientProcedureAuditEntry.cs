namespace MedApp.Domain.Procedures.PatientProcedures;

/// <summary>
/// One change to a procedure: who did what, and when. Written in the same save as the change. It has no foreign
/// key to the procedure, so a deleted procedure's history is kept; the procedure's title and patient are copied in.
/// </summary>
public sealed class PatientProcedureAuditEntry
{
    public Guid Id { get; init; }

    public Guid PatientProcedureId { get; init; }

    /// <summary>The procedure's title at the time, so the entry still reads well after the procedure is deleted.</summary>
    public string ProcedureTitle { get; init; } = string.Empty;

    public Guid PatientId { get; init; }

    /// <summary>The stage the change was about, if any.</summary>
    public Guid? StageId { get; init; }

    /// <summary>The stage's name at the time, if any.</summary>
    public string? StageName { get; init; }

    public PatientProcedureAuditAction Action { get; init; }

    public Guid UserId { get; init; }

    /// <summary>The username at the time, kept even if the account is later deleted.</summary>
    public string Username { get; init; } = string.Empty;

    public DateTime AtUtc { get; init; }

    /// <summary>What changed, as JSON: from/to values, the notes text, a reason, and so on.</summary>
    public string Details { get; init; } = "{}";
}
