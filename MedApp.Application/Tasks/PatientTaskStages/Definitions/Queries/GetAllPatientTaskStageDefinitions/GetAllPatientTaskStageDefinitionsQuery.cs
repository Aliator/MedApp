using MedApp.Contracts.Tasks.PatientTaskStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Queries.GetAllPatientTaskStageDefinitions;

public sealed record GetAllPatientTaskStageDefinitionsQuery : IRequest<IReadOnlyList<PatientTaskStageDefinitionResponse>>;