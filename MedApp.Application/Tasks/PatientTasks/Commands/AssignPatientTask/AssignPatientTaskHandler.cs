using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MedApp.Domain.Tasks.PatientTasks;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Commands.AssignPatientTask;

public sealed class AssignPatientTaskHandler(IPatientTaskRepository repository)
    : IRequestHandler<AssignPatientTaskCommand, List<PatientTaskAssignmentResponse>?>
{
    public async Task<List<PatientTaskAssignmentResponse>?> Handle(
        AssignPatientTaskCommand request,
        CancellationToken ct)
    {
        var task = await repository.GetByIdAsync(request.PatientTaskId, ct);
        if (task is null) return null;

        var now = DateTime.UtcNow;

        if (task.Assignments.All(x => x.UserId != request.UserId))
        {
            task.Assignments.Add(new PatientTaskAssignment
            {
                PatientTaskId = task.Id,
                UserId = request.UserId,
                AssignedByUserId = request.AssignedByUserId,
                AssignedAtUtc = now
            });

            task.LastUpdated = now;
            await repository.UpdateAsync(task, ct);
        }

        return task.Assignments
            .Select(x => new PatientTaskAssignmentResponse
            {
                PatientTaskId = x.PatientTaskId,
                UserId = x.UserId,
                AssignedByUserId = x.AssignedByUserId,
                AssignedAtUtc = x.AssignedAtUtc
            })
            .ToList();
    }
}