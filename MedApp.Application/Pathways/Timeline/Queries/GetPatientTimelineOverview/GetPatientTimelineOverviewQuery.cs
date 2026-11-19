using MedApp.Contracts.Pathways.Timeline.Responses;
using MediatR;

namespace MedApp.Application.Pathways.Timeline.Queries.GetPatientTimelineOverview;

/// <summary>
/// What sits above a patient's timeline: their pathways that aren't closed, soonest due first, and every pathway
/// to filter the timeline by. Deleted pathways are listed only when <paramref name="FullView"/> (see
/// <c>GetPatientTimelineQuery</c>). Only pathways the viewer sees in full are listed (the team walls,
/// <paramref name="Scope"/>; null only outside a request, which is unscoped); the rest are summary lines.
/// </summary>
public sealed record GetPatientTimelineOverviewQuery(
    Guid PatientId, bool FullView, MedApp.Application.Common.Teams.TeamScope? Scope = null)
    : IRequest<PatientTimelineOverviewResponse>
{
    /// <summary>The viewer's team walls: the request's, or the system's outside a request.</summary>
    public MedApp.Application.Common.Teams.TeamScope Teams => Scope ?? MedApp.Application.Common.Teams.TeamScope.System;
}
