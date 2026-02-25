using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.DeletePatientTaskStageDefinition;

public sealed record DeletePatientTaskStageCommand(Guid StageDefinitionId) : IRequest<bool>;