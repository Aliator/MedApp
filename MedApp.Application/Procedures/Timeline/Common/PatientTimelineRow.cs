using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Timeline.Common;

/// <summary>
/// An audit entry on the timeline, with its procedure's title now: null when the procedure has been deleted, or when
/// the entry is about the patient's own file rather than a procedure (<see cref="IsAboutPatient"/>).
/// </summary>
public sealed record PatientTimelineRow(PatientProcedureAuditEntry Entry, string? CurrentProcedureTitle)
{
    /// <summary>About a file attached to the patient themselves, not to one of their procedures.</summary>
    public bool IsAboutPatient => Entry.PatientProcedureId == PatientProcedureAuditEntry.NoProcedure;
}
