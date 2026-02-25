using MedApp.Contracts.Tasks.PatientTaskStageDefinitions.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.CreatePatientTaskStageDefinition;

public sealed record CreatePatientTaskStageCommand(
    string Name,
    string Description,
    string Instructions
) : IRequest<PatientTaskStageDefinitionResponse>;