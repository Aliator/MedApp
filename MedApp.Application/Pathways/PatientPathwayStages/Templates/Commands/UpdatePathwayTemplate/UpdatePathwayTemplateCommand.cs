using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.UpdatePathwayTemplate;

/// <summary>
/// Renames a template, moves it to another team and/or replaces its stage list; null fields are left as they
/// are. Pathways already created from it keep their stages, their clock-stop stage and their team.
/// <paramref name="ClockStopStageOrder"/> goes with the stage list (positions mean nothing without it): the position
/// of the stage that stops the waiting-time clock, or null for none.
/// Each stage may have options (<paramref name="StageOptions"/>: optional, branch point, branch; null for plain stages).
/// <paramref name="RetentionClass"/> (a <c>RetentionClass</c> by name) changes the template's class and, unlike its
/// other fields, raises the class of the pathways already made from it when the new one is longer (never lowers it).
/// </summary>
public sealed record UpdatePathwayTemplateCommand(
    Guid TemplateId,
    string? Name,
    Guid? TeamId,
    IReadOnlyList<Guid>? StageDefinitionIdsInOrder,
    int? ClockStopStageOrder = null,
    IReadOnlyList<TemplateStageOptions>? StageOptions = null,
    string? RetentionClass = null)
    : IRequest<PathwayTemplateResponse>;
