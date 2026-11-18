using MedApp.Contracts.Pathways.Insights.Responses;
using MediatR;

namespace MedApp.Application.Pathways.Insights.Queries.GetPathwayInsights;

/// <summary>
/// The admins' overview of pathway work over the last <paramref name="Weeks"/> UK weeks (this one included),
/// for every pathway or only those made from <paramref name="TemplateId"/>, in <paramref name="TeamId"/>, or both.
/// A template or team that doesn't exist is a 404.
/// </summary>
public sealed record GetPathwayInsightsQuery(int Weeks, Guid? TemplateId, Guid? TeamId = null) : IRequest<PathwayInsightsResponse>
{
    public const int DefaultWeeks = 12;
    public const int MaxWeeks = 52;
}
