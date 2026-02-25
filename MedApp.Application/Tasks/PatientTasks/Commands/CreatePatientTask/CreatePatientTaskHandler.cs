using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MedApp.Domain.Tasks.PatientTasks;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Commands.CreatePatientTask;

public sealed class CreatePatientTaskHandler(
    IPatientTaskRepository taskRepository,
    IPatientTaskStagesRepository stagesRepository,
    IPatientRepository patientRepository)
    : IRequestHandler<CreatePatientTaskCommand, PatientTaskResponse>
{
    public async Task<PatientTaskResponse> Handle(
        CreatePatientTaskCommand request,
        CancellationToken cancellationToken)
    {
        var patient = await patientRepository.GetByIdAsync(request.PatientId, cancellationToken);
        if (patient is null)
            throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        var stageDefinitionIds = request.StageDefinitionIdsInOrder.Distinct().ToList();
        var allDefinitions = await stagesRepository.GetAllStageDefinitionsAsync(cancellationToken);
        var defsById = allDefinitions.ToDictionary(x => x.Id);

        var missingStageDefinitionIds = stageDefinitionIds
            .Where(id => !defsById.ContainsKey(id))
            .ToList();

        if (missingStageDefinitionIds.Count != 0)
            throw new NotFoundException($"Stage definition(s) not found: {string.Join(", ", missingStageDefinitionIds)}.");
        
        var priority = Enum.Parse<PatientTaskPriority>(request.Priority, true);
        var now = DateTime.UtcNow;

        var task = new PatientTask
        {
            Id = Guid.NewGuid(),
            PatientId = request.PatientId,
            Title = request.Title,
            Notes = request.Notes,
            DueDateUtc = request.DueDateUtc,
            Priority = priority,
            Status = PatientTaskStatus.Unassigned,
            CreatedAt = now,
            LastUpdated = now,
            Stages = stageDefinitionIds
                .Distinct()
                .Select((stageDefinitionId, index) => new PatientTaskStage
                {
                    Id = Guid.NewGuid(),
                    StageDefinitionId = stageDefinitionId,
                    StageOrder = index + 1,
                    CreatedAt = now,
                    LastUpdated = now
                })
                .ToList(),
            Assignments = []
        };

        await taskRepository.AddAsync(task, cancellationToken);

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