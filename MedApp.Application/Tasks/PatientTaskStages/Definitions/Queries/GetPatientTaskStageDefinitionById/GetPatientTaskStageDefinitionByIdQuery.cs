using MedApp.Contracts.Tasks.PatientTaskStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Queries.GetPatientTaskStageDefinitionById;

public sealed record GetPatientTaskStageDefinitionByIdQuery(Guid StageDefinitionId) : IRequest<PatientTaskStageDefinitionResponse>;