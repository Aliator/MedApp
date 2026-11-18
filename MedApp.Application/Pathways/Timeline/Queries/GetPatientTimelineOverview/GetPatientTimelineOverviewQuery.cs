using MedApp.Contracts.Pathways.Timeline.Responses;
using MediatR;

namespace MedApp.Application.Pathways.Timeline.Queries.GetPatientTimelineOverview;

/// <summary>
/// What sits above a patient's timeline: their pathways that aren't closed, soonest due first, and every pathway
/// to filter the timeline by. Deleted pathways are listed only when <paramref name="IsAdmin"/> (see
/// <c>GetPatientTimelineQuery</c>).
/// </summary>
public sealed record GetPatientTimelineOverviewQuery(Guid PatientId, bool IsAdmin)
    : IRequest<PatientTimelineOverviewResponse>;
