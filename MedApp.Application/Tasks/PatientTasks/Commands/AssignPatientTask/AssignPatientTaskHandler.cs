using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MedApp.Domain.Tasks.PatientTasks;
using MediatR;
 
namespace MedApp.Application.Tasks.PatientTasks.Commands.AssignPatientTask;

public sealed class AssignPatientTaskHandler(IPatientTaskRepository repository)
    : IRequestHandler<AssignPatientTaskCommand, PatientTaskResponse?>
{
    public async Task<PatientTaskResponse?> Handle(
        AssignPatientTaskCommand request,
        CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.PatientTaskId, cancellationToken);

        if (task is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var assignedIds = request.AssignedUserIds.Distinct().ToHashSet();

        task.Assignments = task.Assignments
            .Where(x => assignedIds.Contains(x.UserId))
            .ToList();

        foreach (var userId in assignedIds)
        {
            if (task.Assignments.Any(x => x.UserId == userId))
            {
                continue;
            }

            task.Assignments.Add(new PatientTaskAssignment
            {
                PatientTaskId = task.Id,
                UserId = userId,
                AssignedByUserId = request.AssignedByUserId,
                AssignedAtUtc = now
            });
        }

        task.Status = task.Assignments.Count == 0
            ? PatientTaskStatus.Unassigned
            : task.Status == PatientTaskStatus.Unassigned
                ? PatientTaskStatus.NotStarted
                : task.Status;

        task.LastUpdated = now;

        await repository.UpdateAsync(task, cancellationToken);

        return new PatientTaskResponse
        {
            Id = task.Id,
            PatientId = task.PatientId,
            Title = task.Title,
            Notes = task.Notes,
            DueDateUtc = task.DueDateUtc,
            Priority = task.Priority.ToString(),
            Status = task.Status.ToString(),
            CreatedAt = task.CreatedAt,
            LastUpdated = task.LastUpdated,
            Stages = task.Stages
                .OrderBy(x => x.StageOrder)
                .Select(x => new PatientTaskStageResponse
                {
                    Id = x.Id,
                    StageDefinitionId = x.StageDefinitionId,
                    StageOrder = x.StageOrder,
                    IsCompleted = x.IsCompleted,
                    CompletedAtUtc = x.CompletedAtUtc,
                    CompletedByUserId = x.CompletedByUserId,
                    StageName = x.StageDefinition?.Name ?? string.Empty,
                    StageDescription = x.StageDefinition?.Description ?? string.Empty,
                    StageInstructions = x.StageDefinition?.Instructions ?? string.Empty
                })
                .ToList(),
            Assignments = task.Assignments
                .Select(x => new PatientTaskAssignmentResponse
                {
                    UserId = x.UserId,
                    AssignedByUserId = x.AssignedByUserId,
                    AssignedAtUtc = x.AssignedAtUtc
                })
                .ToList()
        };
    }
}