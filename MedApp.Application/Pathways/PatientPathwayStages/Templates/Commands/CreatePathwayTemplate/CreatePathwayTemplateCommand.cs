using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;

/// <summary>
/// Creates a template in a specialty: an ordered list of stages, in which a stage may appear more than once, and
/// optionally the position (from 1) of the stage whose finishing stops a referral's waiting-time clock.
/// </summary>
public sealed record CreatePathwayTemplateCommand(
    string Name,
    Guid SpecialtyId,
    IReadOnlyList<Guid> StageDefinitionIdsInOrder,
    int? ClockStopStageOrder = null)
    : IRequest<PathwayTemplateResponse>;
