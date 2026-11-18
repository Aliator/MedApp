using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;

/// <summary>
/// Creates a template in a specialty: an ordered list of stages, in which a stage may appear more than once, and
/// optionally the position (from 1) of the stage whose finishing stops a referral's waiting-time clock.
/// Each stage may have options (<paramref name="StageOptions"/>: optional, branch point, branch; null for plain stages).
/// <paramref name="RetentionClass"/> is a <c>RetentionClass</c> by name (null for Standard).
/// </summary>
public sealed record CreatePathwayTemplateCommand(
    string Name,
    Guid SpecialtyId,
    IReadOnlyList<Guid> StageDefinitionIdsInOrder,
    int? ClockStopStageOrder = null,
    IReadOnlyList<TemplateStageOptions>? StageOptions = null,
    string? RetentionClass = null)
    : IRequest<PathwayTemplateResponse>;
