using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Commands.AssignPatientTask;

public sealed record AssignPatientTaskCommand(
    Guid PatientTaskId,
    IReadOnlyList<Guid> AssignedUserIds,
    Guid AssignedByUserId
) : IRequest<PatientTaskResponse?>;