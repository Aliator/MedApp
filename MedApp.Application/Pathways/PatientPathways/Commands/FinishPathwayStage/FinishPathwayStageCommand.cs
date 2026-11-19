using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;

/// <summary>
/// Finishes and locks a stage, moving the pathway on to its next stage (or to review after the last). A stage whose
/// visit is Required needs an Attended appointment first, unless a holder of FinishWithoutVisit (<paramref name="CanFinishWithoutVisit"/>) finishes it
/// <paramref name="WithoutVisit"/>, giving a <paramref name="Reason"/>. A choice node finished for the first time needs
/// the clinician's <paramref name="Outcome"/>, and grows the pathway by that outcome's stages (MC10b). The team that
/// works the stage finishes it (MC7: the lab its own, by hand).
/// </summary>
public sealed record FinishPathwayStageCommand(
    Guid PathwayId,
    Guid StageId,
    PathwayActor Actor,
    bool WithoutVisit = false,
    string? Reason = null,
    bool CanFinishWithoutVisit = false,
    string? Outcome = null)
    : IRequest<PatientPathwayResponse>;
