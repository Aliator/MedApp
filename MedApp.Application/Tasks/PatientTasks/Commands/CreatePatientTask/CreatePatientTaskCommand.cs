using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Commands.CreatePatientTask;

public sealed record CreatePatientTaskCommand(
    Guid PatientId,
    string Title,
    string Notes,
    DateTime DueDateUtc,
    string Priority,
    IReadOnlyList<Guid> StageDefinitionIdsInOrder
) : IRequest<PatientTaskResponse>;