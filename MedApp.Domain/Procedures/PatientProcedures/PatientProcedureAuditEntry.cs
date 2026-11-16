namespace MedApp.Domain.Procedures.PatientProcedures;

/// <summary>
/// One change to a procedure: who did what, and when. Written in the same save as the change. It has no foreign
/// key to the procedure, so a deleted procedure's history is kept; the procedure's title and patient are copied in.
/// </summary>
public sealed class PatientProcedureAuditEntry
{
    /// <summary>
    /// The <see cref="PatientProcedureId"/> of an entry about the patient rather than one of their procedures: a file
    /// attached to the patient themselves. Such entries have no procedure title or stage.
    /// </summary>
    public static readonly Guid NoProcedure = Guid.Empty;

    public Guid Id { get; init; }

    /// <summary>The procedure it's about, or <see cref="NoProcedure"/> for an entry about the patient's own file.</summary>
    public Guid PatientProcedureId { get; init; }

    /// <summary>The procedure's title at the time, so the entry still reads well after the procedure is deleted.</summary>
    public string ProcedureTitle { get; set; } = string.Empty;

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
    public string Details { get; set; } = "{}";
}
