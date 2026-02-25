using MedApp.Contracts.Tasks.PatientTaskStageDefinitions.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Queries.GetAllPatientTaskStageDefinitions;

public sealed record GetAllPatientTaskStagesQuery : IRequest<IReadOnlyList<PatientTaskStageDefinitionResponse>>;