using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MedApp.Domain.Tasks.PatientTasks;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Commands.AssignPatientTask;

public sealed class AssignPatientTaskHandler(
    IPatientTaskRepository repository,
    IIdentityReadService identityReadService)
    : IRequestHandler<AssignPatientTaskCommand, List<PatientTaskAssignmentResponse>>
{
    public async Task<List<PatientTaskAssignmentResponse>> Handle(
        AssignPatientTaskCommand request,
        CancellationToken ct)
    {
        var task = await repository.GetByIdAsync(request.PatientTaskId, ct)
                   ?? throw new NotFoundException($"Patient task '{request.PatientTaskId}' was not found.");

        var users = await identityReadService.GetUsersAsync(ct);
        if (users.All(u => u.UserId != request.UserId))
            throw new NotFoundException($"User '{request.UserId}' was not found.");

        if (task.Assignments.Any(x => x.UserId == request.UserId))
            throw new ConflictException("This user is already assigned to the patient task.");

        var now = DateTime.UtcNow;
       
        task.Assignments.Add(new PatientTaskAssignment
        {
            PatientTaskId = task.Id,
            UserId = request.UserId,
            AssignedByUserId = request.AssignedByUserId,
            AssignedAtUtc = now
            });
        
        if (task.Status == PatientTaskStatus.Unassigned)
        {
            task.Status = PatientTaskStatus.Assigned;
        }
        
        task.LastUpdated = now;
       
        await repository.UpdateAsync(task, ct);

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