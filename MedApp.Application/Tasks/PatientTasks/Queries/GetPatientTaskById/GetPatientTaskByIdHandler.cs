using MedApp.Application.Common.Exceptions;
using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Queries.GetPatientTaskById;

public sealed class GetPatientTaskByIdHandler(IPatientTaskRepository repository)
    : IRequestHandler<GetPatientTaskByIdQuery, PatientTaskResponse>
{
    public async Task<PatientTaskResponse> Handle(
        GetPatientTaskByIdQuery request,
        CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.PatientTaskId, cancellationToken)
                   ?? throw new NotFoundException($"Patient task '{request.PatientTaskId}' was not found.");

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
                    PatientTaskId = x.PatientTaskId,
                    UserId = x.UserId,
                    AssignedByUserId = x.AssignedByUserId,
                    AssignedAtUtc = x.AssignedAtUtc
                })
                .ToList()
        };
    }
}