using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;

public sealed class CreatePatientProcedureHandler(
    IPatientProcedureRepository procedureRepository,
    IPatientProcedureStagesRepository stagesRepository,
    IPatientRepository patientRepository)
    : IRequestHandler<CreatePatientProcedureCommand, PatientProcedureResponse>
{
    public async Task<PatientProcedureResponse> Handle(
        CreatePatientProcedureCommand request,
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
        
        var priority = Enum.Parse<PatientProcedurePriority>(request.Priority, true);
        var now = DateTime.UtcNow;

        var procedure = new PatientProcedure
        {
            Id = Guid.NewGuid(),
            PatientId = request.PatientId,
            Title = request.Title,
            Notes = request.Notes,
            DueDateUtc = request.DueDateUtc,
            Priority = priority,
            Status = PatientProcedureStatus.Unassigned,
            CreatedAt = now,
            LastUpdated = now,
            Stages = stageDefinitionIds
                .Distinct()
                .Select((stageDefinitionId, index) => new PatientProcedureStage
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

        await procedureRepository.AddAsync(procedure, cancellationToken);

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