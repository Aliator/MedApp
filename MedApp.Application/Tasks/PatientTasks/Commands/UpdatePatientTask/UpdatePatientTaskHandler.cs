using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MedApp.Domain.Tasks.PatientTasks;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Commands.UpdatePatientTask;

public sealed class UpdatePatientTaskHandler(IPatientTaskRepository repository)
    : IRequestHandler<UpdatePatientTaskCommand, PatientTaskResponse?>
{
    public async Task<PatientTaskResponse?> Handle(
        UpdatePatientTaskCommand request,
        CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.PatientTaskId, cancellationToken);

        if (task is null)
        {
            return null;
        }

        if (request.Title is not null)
        {
            task.Title = request.Title;
        }

        if (request.Notes is not null)
        {
            task.Notes = request.Notes;
        }

        if (request.DueDateUtc.HasValue)
        {
            task.DueDateUtc = request.DueDateUtc.Value;
        }

        if (request.Priority is not null)
        {
            task.Priority = Enum.Parse<PatientTaskPriority>(request.Priority, true);
        }

        if (request.Status is not null)
        {
            task.Status = Enum.Parse<PatientTaskStatus>(request.Status, true);
        }

        if (request.StageDefinitionIdsInOrder is not null)
        {
            var now = DateTime.UtcNow;
            var stageIds = request.StageDefinitionIdsInOrder.Distinct().ToList();

            var existingStagesByDefinition = task.Stages
                .ToDictionary(x => x.StageDefinitionId);

            var nextStages = new List<PatientTaskStage>(stageIds.Count);
            for (var index = 0; index < stageIds.Count; index++)
            {
                var stageDefinitionId = stageIds[index];
                if (existingStagesByDefinition.TryGetValue(stageDefinitionId, out var existingStage))
                {
                    existingStage.StageOrder = index + 1;
                    existingStage.LastUpdated = now;
                    nextStages.Add(existingStage);
                    continue;
                }

                nextStages.Add(new PatientTaskStage
                {
                    Id = Guid.NewGuid(),
                    PatientTaskId = task.Id,
                    StageDefinitionId = stageDefinitionId,
                    StageOrder = index + 1,
                    CreatedAt = now,
                    LastUpdated = now
                });
            }

            task.Stages = nextStages;
        }

        task.LastUpdated = DateTime.UtcNow;

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