using MedApp.Application.Common.Teams;
using MedApp.Contracts.Pathways.Insights.Responses;
using MediatR;

namespace MedApp.Application.Pathways.Insights.Queries.GetPathwayInsights;

/// <summary>
/// The admins' overview of pathway work over the last <paramref name="Weeks"/> UK weeks (this one included),
/// for every pathway or only those made from <paramref name="TemplateId"/>, in <paramref name="TeamId"/>, or both.
/// A template or team that doesn't exist is a 404. Only the viewer's teams' pathways count (<see cref="Scope"/>), unless
/// they see every team (admins); <paramref name="TeamId"/> (the clinic picker) must be one of theirs (else a 400), or any
/// team for an admin.
/// </summary>
public sealed record GetPathwayInsightsQuery(int Weeks, Guid? TemplateId, Guid? TeamId = null, TeamScope? Scope = null)
    : IRequest<PathwayInsightsResponse>
{
    /// <summary>The viewer's team walls (null only outside a request: the system's).</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;

    public const int DefaultWeeks = 12;
    public const int MaxWeeks = 52;
}
