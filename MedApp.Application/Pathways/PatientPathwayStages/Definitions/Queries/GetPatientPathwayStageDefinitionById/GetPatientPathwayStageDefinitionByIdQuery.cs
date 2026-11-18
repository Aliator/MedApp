using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions.Queries.GetPatientPathwayStageDefinitionById;

public sealed record GetPatientPathwayStageDefinitionByIdQuery(Guid StageDefinitionId)
    : IRequest<PatientPathwayStageDefinitionResponse>;
