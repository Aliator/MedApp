using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.DeletePatientTaskStageDefinition;

public sealed record DeletePatientTaskStageDefinitionCommand(Guid StageDefinitionId) : IRequest<bool>;