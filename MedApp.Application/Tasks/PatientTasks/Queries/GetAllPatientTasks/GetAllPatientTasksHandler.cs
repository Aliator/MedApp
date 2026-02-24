using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Queries.GetAllPatientTasks;

public sealed class GetAllPatientTasksHandler(IPatientTaskRepository repository)
    : IRequestHandler<GetAllPatientTasksQuery, IReadOnlyList<PatientTaskResponse>>
{
    public async Task<IReadOnlyList<PatientTaskResponse>> Handle(
        GetAllPatientTasksQuery request,
        CancellationToken cancellationToken)
    {
        var tasks = await repository.GetAllAsync(cancellationToken);

        return tasks
            .Select(task => new PatientTaskResponse
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
            })
            .ToList();
    }
}