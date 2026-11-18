using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.UpdatePathwayTemplate;

/// <summary>
/// Renames a template, moves it to another specialty and/or replaces its stage list; null fields are left as they
/// are. Pathways already created from it keep their stages and their specialty.
/// </summary>
public sealed record UpdatePathwayTemplateCommand(
    Guid TemplateId,
    string? Name,
    Guid? SpecialtyId,
    IReadOnlyList<Guid>? StageDefinitionIdsInOrder)
    : IRequest<PathwayTemplateResponse>;
