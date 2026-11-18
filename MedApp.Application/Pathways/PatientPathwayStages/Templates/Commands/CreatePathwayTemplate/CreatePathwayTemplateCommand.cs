using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;

/// <summary>
/// Creates a template in a specialty: an ordered list of stages, in which a stage may appear more than once, each
/// with its options (<paramref name="StageOptions"/>: optional, branch point, branch; null for plain stages).
/// </summary>
public sealed record CreatePathwayTemplateCommand(
    string Name,
    Guid SpecialtyId,
    IReadOnlyList<Guid> StageDefinitionIdsInOrder,
    IReadOnlyList<TemplateStageOptions>? StageOptions = null)
    : IRequest<PathwayTemplateResponse>;
