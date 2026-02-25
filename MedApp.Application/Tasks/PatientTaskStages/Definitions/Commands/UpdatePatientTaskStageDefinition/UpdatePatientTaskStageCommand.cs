using MedApp.Contracts.Tasks.PatientTaskStageDefinitions.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.UpdatePatientTaskStageDefinition;

public sealed record UpdatePatientTaskStageCommand(
    Guid StageDefinitionId,
    string? Name,
    string? Description,
    string? Instructions
) : IRequest<PatientTaskStageDefinitionResponse>;