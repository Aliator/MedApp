using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MedApp.Domain.Tasks.PatientTasks;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Commands.CreatePatientTask;

public sealed class CreatePatientTaskHandler(
    IPatientTaskRepository taskRepository)
    : IRequestHandler<CreatePatientTaskCommand, PatientTaskResponse>
{
    public async Task<PatientTaskResponse> Handle(
        CreatePatientTaskCommand request,
        CancellationToken cancellationToken)
    {
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
            Stages = request.StageDefinitionIdsInOrder
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
            Stages = [],
            Assignments = []
        };
    }
}