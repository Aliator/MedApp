using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.UpdatePathwayStageNotes;

/// <summary>Replaces a stage's notes: the current stage, or a finished one an admin has unlocked.</summary>
public sealed record UpdatePathwayStageNotesCommand(Guid PathwayId, Guid StageId, string Notes, PathwayActor Actor)
    : IRequest<PatientPathwayResponse>;
