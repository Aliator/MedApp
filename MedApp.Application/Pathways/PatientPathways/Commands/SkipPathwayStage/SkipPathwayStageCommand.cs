using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.SkipPathwayStage;

/// <summary>
/// Skips an optional stage while it's current, with a reason by name (NotNeeded, PatientDeclined or Other) and a
/// detail (required for Other): the pathway moves on to its next stage, as if it had been finished. A stage whose
/// visit is Required can be skipped only by a holder of FinishWithoutVisit (<paramref name="CanFinishWithoutVisit"/>), with a
/// detail, as finishing it without its visit can.
/// </summary>
public sealed record SkipPathwayStageCommand(
    Guid PathwayId, Guid StageId, string Reason, string? Detail, PathwayActor Actor, bool CanFinishWithoutVisit = false)
    : IRequest<PatientPathwayResponse>;
