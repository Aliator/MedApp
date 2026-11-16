using MedApp.Contracts.Procedures.Insights.Responses;
using MediatR;

namespace MedApp.Application.Procedures.Insights.Queries.GetProcedureInsights;

/// <summary>
/// The admins' overview of procedure work over the last <paramref name="Weeks"/> UK weeks (this one included),
/// for every procedure or only those made from <paramref name="TemplateId"/>. A template that doesn't exist is a 404.
/// </summary>
public sealed record GetProcedureInsightsQuery(int Weeks, Guid? TemplateId) : IRequest<ProcedureInsightsResponse>
{
    public const int DefaultWeeks = 12;
    public const int MaxWeeks = 52;
}
