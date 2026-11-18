using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.UnlockPathwayStage;

/// <summary>Reopens a finished stage so it can be corrected and finished again (admins only).</summary>
public sealed record UnlockPathwayStageCommand(Guid PathwayId, Guid StageId, PathwayActor Actor)
    : IRequest<PatientPathwayResponse>;
