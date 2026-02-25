using MedApp.Contracts.Tasks.PatientTaskStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.UpdatePatientTaskStageDefinition;

public sealed record UpdatePatientTaskStageDefinitionCommand(
    Guid StageDefinitionId,
    string? Name,
    string? Description,
    string? Instructions
) : IRequest<PatientTaskStageDefinitionResponse>;