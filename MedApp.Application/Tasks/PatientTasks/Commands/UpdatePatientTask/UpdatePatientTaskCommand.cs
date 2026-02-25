using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Commands.UpdatePatientTask;

public sealed record UpdatePatientTaskCommand(
    Guid PatientTaskId,
    string? Title,
    string? Notes,
    DateTime? DueDateUtc,
    string? Priority,
    string? Status,
    IReadOnlyList<Guid>? StageDefinitionIdsInOrder
) : IRequest<PatientTaskResponse>;