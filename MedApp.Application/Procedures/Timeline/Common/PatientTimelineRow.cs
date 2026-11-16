using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Timeline.Common;

/// <summary>An audit entry on the timeline, with its procedure's title now: null when the procedure has been deleted.</summary>
public sealed record PatientTimelineRow(PatientProcedureAuditEntry Entry, string? CurrentProcedureTitle);
