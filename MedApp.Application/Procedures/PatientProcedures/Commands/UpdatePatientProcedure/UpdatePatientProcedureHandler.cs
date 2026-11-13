using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;

public sealed class UpdatePatientProcedureHandler(
    IPatientProcedureRepository repository,
    IPatientProcedureStagesRepository stagesRepository)
    : IRequestHandler<UpdatePatientProcedureCommand, PatientProcedureResponse>
{
    public async Task<PatientProcedureResponse> Handle(
        UpdatePatientProcedureCommand request,
        CancellationToken cancellationToken)
    {
        var procedure = await repository.GetByIdAsync(request.PatientProcedureId, cancellationToken)
                   ?? throw new NotFoundException($"Patient procedure '{request.PatientProcedureId}' was not found.");
        
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

            var existingStagesByDefinition = procedure.Stages.ToDictionary(x => x.StageDefinitionId);

            var nextStages = new List<PatientProcedureStage>(stageIds.Count);
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

                nextStages.Add(new PatientProcedureStage
                {
                    Id = Guid.NewGuid(),
                    PatientProcedureId = procedure.Id,
                    StageDefinitionId = stageDefinitionId,
                    StageOrder = index + 1,
                    CreatedAt = now,
                    LastUpdated = now
                });
            }

            procedure.Stages = nextStages;
        }
         
        if (request.Title is not null) procedure.Title = request.Title;
        if (request.Notes is not null) procedure.Notes = request.Notes;
        if (request.DueDateUtc.HasValue) procedure.DueDateUtc = request.DueDateUtc.Value;
        if (request.Priority is not null) procedure.Priority = Enum.Parse<PatientProcedurePriority>(request.Priority, true);
        if (request.Status is not null) procedure.Status = Enum.Parse<PatientProcedureStatus>(request.Status, true);
        

        procedure.LastUpdated = DateTime.UtcNow;

        await repository.UpdateAsync(procedure, cancellationToken);

        return new PatientProcedureResponse
        {
            Id = procedure.Id,
            PatientId = procedure.PatientId,
            Title = procedure.Title,
            Notes = procedure.Notes,
            DueDateUtc = procedure.DueDateUtc,
            Priority = procedure.Priority.ToString(),
            Status = procedure.Status.ToString(),
            CreatedAt = procedure.CreatedAt,
            LastUpdated = procedure.LastUpdated,
            Stages = procedure.Stages
                .OrderBy(x => x.StageOrder)
                .Select(x =>
                {
                    defsById.TryGetValue(x.StageDefinitionId, out var def);

                    return new PatientProcedureStageResponse
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
            Assignments = procedure.Assignments
                .Select(x => new PatientProcedureAssignmentResponse
                {
                    UserId = x.UserId,
                    AssignedByUserId = x.AssignedByUserId,
                    AssignedAtUtc = x.AssignedAtUtc
                })
                .ToList()
        };
    }
}