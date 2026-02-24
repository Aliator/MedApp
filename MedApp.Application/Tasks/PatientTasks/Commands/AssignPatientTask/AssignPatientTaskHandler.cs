using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MedApp.Domain.Tasks.PatientTasks;
using MediatR;
 
namespace MedApp.Application.Tasks.PatientTasks.Commands.AssignPatientTask;

public sealed class AssignPatientTaskHandler(IPatientTaskRepository repository)
    : IRequestHandler<AssignPatientTaskCommand, PatientTaskResponse?>
{
    public async Task<PatientTaskResponse?> Handle(AssignPatientTaskCommand request, CancellationToken ct)
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