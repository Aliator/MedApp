using MedApp.Contracts.Procedures.Timeline.Responses;
using MediatR;

namespace MedApp.Application.Procedures.Timeline.Queries.GetPatientTimelineOverview;

/// <summary>
/// What sits above a patient's timeline: their procedures that aren't closed, soonest due first, and every procedure
/// to filter the timeline by. Deleted procedures are listed only when <paramref name="IsAdmin"/> (see
/// <c>GetPatientTimelineQuery</c>).
/// </summary>
public sealed record GetPatientTimelineOverviewQuery(Guid PatientId, bool IsAdmin)
    : IRequest<PatientTimelineOverviewResponse>;
