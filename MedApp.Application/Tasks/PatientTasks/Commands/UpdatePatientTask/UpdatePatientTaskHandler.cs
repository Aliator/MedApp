using MedApp.Application.Common.Exceptions;
using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MedApp.Domain.Tasks.PatientTasks;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Commands.UpdatePatientTask;

public sealed class UpdatePatientTaskHandler(
    IPatientTaskRepository repository,
    IPatientTaskStagesRepository stagesRepository)
    : IRequestHandler<UpdatePatientTaskCommand, PatientTaskResponse>
{
    public async Task<PatientTaskResponse> Handle(
        UpdatePatientTaskCommand request,
        CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.PatientTaskId, cancellationToken)
                   ?? throw new NotFoundException($"Patient task '{request.PatientTaskId}' was not found.");
        
        var defs = await stagesRepository.GetAllStageDefinitionsAsync(cancellationToken);
        var defsById = defs.ToDictionary(x => x.Id);

        if (request.StageDefinitionIdsInOrder is not null)
        {
            var missingStageDefinitionIds = request.StageDefinitionIdsInOrder
                .Distinct()
                .Where(id => !defsById.ContainsKey(id))
                .ToList();

            if (missingStageDefinitionIds.Count != 0)
                throw new NotFoundException($"Stage definition(s) not found: {string.Join(", ", missingStageDefinitionIds)}.");
            
            var now = DateTime.UtcNow;
            var stageIds = request.StageDefinitionIdsInOrder.Distinct().ToList();

            var existingStagesByDefinition = task.Stages.ToDictionary(x => x.StageDefinitionId);

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
         
        if (request.Title is not null) task.Title = request.Title;
        if (request.Notes is not null) task.Notes = request.Notes;
        if (request.DueDateUtc.HasValue) task.DueDateUtc = request.DueDateUtc.Value;
        if (request.Priority is not null) task.Priority = Enum.Parse<PatientTaskPriority>(request.Priority, true);
        if (request.Status is not null) task.Status = Enum.Parse<PatientTaskStatus>(request.Status, true);
        

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
                .Select(x =>
                {
                    defsById.TryGetValue(x.StageDefinitionId, out var def);

                    return new PatientTaskStageResponse
                    {
                        Id = x.Id,
                        StageDefinitionId = x.StageDefinitionId,
                        StageOrder = x.StageOrder,
                        IsCompleted = x.IsCompleted,
                        CompletedAtUtc = x.CompletedAtUtc,
                        CompletedByUserId = x.CompletedByUserId,
                        StageName = def?.Name ?? string.Empty,
                        StageDescription = def?.Description ?? string.Empty,
                        StageInstructions = def?.Instructions ?? string.Empty
                    };
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