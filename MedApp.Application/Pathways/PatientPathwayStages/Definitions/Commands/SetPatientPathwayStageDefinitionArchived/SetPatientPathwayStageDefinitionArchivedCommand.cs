using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.SetPatientPathwayStageDefinitionArchived;

/// <summary>Archives a stage definition (it can't be added to templates any more) or restores it.</summary>
public sealed record SetPatientPathwayStageDefinitionArchivedCommand(Guid StageDefinitionId, bool IsArchived)
    : IRequest<PatientPathwayStageDefinitionResponse>;
