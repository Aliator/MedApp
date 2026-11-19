using MedApp.Application.Common.Teams;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MediatR;

namespace MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;

/// <summary>
/// One page of a patient's timeline: everything the audit log records about their pathways, and their appointments'
/// history (the Appointments kind), merged newest first, a page at a time after <paramref name="Before"/> (a
/// <c>TimelineCursor</c>). <paramref name="Kinds"/> and <paramref name="PathwayIds"/> are each any of several;
/// filtering by pathways leaves out appointments not for one.
/// <para>
/// <paramref name="FullView"/> (holders of ViewAudit: admins) decides what the caller sees. They see the whole history, deleted pathways and
/// the free-text values changes recorded. Staff see entries only for pathways that still exist (and appointments
/// not for a pathway), and never notes, titles or reasons from the log, as the audit log itself is admin-only.
/// </para>
/// <para>
/// <paramref name="Scope"/> is the viewer's team walls (from the request; null only outside one, which is unscoped):
/// an entry shows when its pathway is one they see in full, or, without one, when its team is theirs. Other teams'
/// entries are left out, not summarised. Admins see every team's.
/// </para>
/// </summary>
public sealed record GetPatientTimelineQuery(
    Guid PatientId,
    string? Before,
    int PageSize,
    IReadOnlyList<string>? Kinds,
    IReadOnlyList<Guid>? PathwayIds,
    bool FullView,
    TeamScope? Scope = null)
    : IRequest<PatientTimelinePageResponse>
{
    /// <summary>The viewer's team walls: the request's, or the system's outside a request.</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;
}
