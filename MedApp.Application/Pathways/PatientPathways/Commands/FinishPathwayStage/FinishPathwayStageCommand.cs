using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;

/// <summary>
/// Finishes and locks a stage, moving the pathway on to its next stage (or to review after the last). A stage whose
/// visit is Required needs an Attended appointment first, unless a holder of OverrideStages (<paramref name="CanOverrideStages"/>) finishes it
/// <paramref name="WithoutVisit"/>, giving a <paramref name="Reason"/>. A branch point finished for the first time needs
/// the clinician's <paramref name="Branch"/>, and skips the other branches' stages.
/// </summary>
public sealed record FinishPathwayStageCommand(
    Guid PathwayId,
    Guid StageId,
    PathwayActor Actor,
    bool WithoutVisit = false,
    string? Reason = null,
    bool CanOverrideStages = false,
    string? Branch = null)
    : IRequest<PatientPathwayResponse>;
