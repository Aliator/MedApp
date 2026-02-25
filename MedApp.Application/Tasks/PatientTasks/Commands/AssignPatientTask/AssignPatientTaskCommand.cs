using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Commands.AssignPatientTask;

public sealed record AssignPatientTaskCommand(
    Guid PatientTaskId,
    Guid UserId,
    Guid AssignedByUserId)
    : IRequest<List<PatientTaskAssignmentResponse>?>;