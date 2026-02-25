using MedApp.Contracts.Tasks.PatientTaskStageDefinitions.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Queries.GetPatientTaskStageDefinitionById;

public sealed record GetPatientTaskStageByIdQuery(Guid StageDefinitionId) : IRequest<PatientTaskStageDefinitionResponse>;