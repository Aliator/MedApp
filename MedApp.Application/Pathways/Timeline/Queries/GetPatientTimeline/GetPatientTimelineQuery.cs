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
/// </summary>
public sealed record GetPatientTimelineQuery(
    Guid PatientId,
    string? Before,
    int PageSize,
    IReadOnlyList<string>? Kinds,
    IReadOnlyList<Guid>? PathwayIds,
    bool FullView)
    : IRequest<PatientTimelinePageResponse>;
