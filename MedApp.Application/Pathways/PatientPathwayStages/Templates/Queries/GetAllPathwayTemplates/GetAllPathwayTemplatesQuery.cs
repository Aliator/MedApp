using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Queries.GetAllPathwayTemplates;

/// <summary>Every template with its stages, sorted by name; archived ones only when asked for.</summary>
public sealed record GetAllPathwayTemplatesQuery(bool IncludeArchived = false)
    : IRequest<IReadOnlyList<PathwayTemplateResponse>>;
