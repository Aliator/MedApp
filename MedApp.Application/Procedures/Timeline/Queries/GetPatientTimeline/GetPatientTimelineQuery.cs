using MedApp.Contracts.Procedures.Timeline.Responses;
using MediatR;

namespace MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;

/// <summary>
/// One page of a patient's timeline: everything the audit log records about their procedures, and their appointments'
/// history (the Appointments kind), merged newest first, a page at a time after <paramref name="Before"/> (a
/// <c>TimelineCursor</c>). <paramref name="Kinds"/> and <paramref name="ProcedureIds"/> are each any of several;
/// filtering by procedures leaves out appointments not for one.
/// <para>
/// <paramref name="IsAdmin"/> decides what the caller sees. Admins see the whole history, deleted procedures and
/// the free-text values changes recorded. Staff see entries only for procedures that still exist (and appointments
/// not for a procedure), and never notes, titles or reasons from the log, as the audit log itself is admin-only.
/// </para>
/// </summary>
public sealed record GetPatientTimelineQuery(
    Guid PatientId,
    string? Before,
    int PageSize,
    IReadOnlyList<string>? Kinds,
    IReadOnlyList<Guid>? ProcedureIds,
    bool IsAdmin)
    : IRequest<PatientTimelinePageResponse>;
