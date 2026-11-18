using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.UpdatePathwayTemplate;

/// <summary>Renames a template and/or replaces its stage list. Pathways already created from it keep their stages.</summary>
public sealed record UpdatePathwayTemplateCommand(
    Guid TemplateId,
    string? Name,
    IReadOnlyList<Guid>? StageDefinitionIdsInOrder)
    : IRequest<PathwayTemplateResponse>;
