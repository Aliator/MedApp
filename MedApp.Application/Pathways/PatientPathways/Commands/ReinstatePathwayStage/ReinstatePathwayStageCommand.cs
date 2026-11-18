using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.ReinstatePathwayStage;

/// <summary>
/// Reinstates a skipped stage, with a reason (holders of OverrideStages): it runs again in its place, so if it's before
/// the current stage it becomes current, as unlocking reopens a stage. A branch can be changed only this way.
/// </summary>
public sealed record ReinstatePathwayStageCommand(Guid PathwayId, Guid StageId, string Reason, PathwayActor Actor)
    : IRequest<PatientPathwayResponse>;
