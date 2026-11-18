using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.StartPathwayStage;

/// <summary>Starts work on the current stage. Saving notes or finishing a stage also starts it.</summary>
public sealed record StartPathwayStageCommand(Guid PathwayId, Guid StageId, PathwayActor Actor)
    : IRequest<PatientPathwayResponse>;
