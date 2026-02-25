using MedApp.Contracts.Tasks.PatientTaskStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.CreatePatientTaskStageDefinition;

public sealed record CreatePatientTaskStageDefinitionCommand(
    string Name,
    string Description,
    string Instructions
) : IRequest<PatientTaskStageDefinitionResponse>;