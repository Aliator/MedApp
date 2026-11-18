using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;

/// <summary>
/// Creates a template in a specialty: an ordered list of stages, in which a stage may appear more than once.
/// </summary>
public sealed record CreatePathwayTemplateCommand(
    string Name,
    Guid SpecialtyId,
    IReadOnlyList<Guid> StageDefinitionIdsInOrder)
    : IRequest<PathwayTemplateResponse>;
