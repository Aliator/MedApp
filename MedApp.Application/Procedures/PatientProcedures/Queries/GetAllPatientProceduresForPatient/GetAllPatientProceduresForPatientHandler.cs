using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.GetAllPatientProceduresForPatient;

public sealed class GetAllPatientProceduresForPatientHandler(IPatientProcedureRepository repository)
    : IRequestHandler<GetAllPatientProceduresForPatientQuery, IReadOnlyList<PatientProcedureResponse>>
{
    public async Task<IReadOnlyList<PatientProcedureResponse>> Handle(
        GetAllPatientProceduresForPatientQuery request,
        CancellationToken cancellationToken)
    {
        
        var procedures = (await repository.GetAllForPatientAsync(request.PatientId, cancellationToken)).ToList();
        if (procedures.Count == 0)
            throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        return procedures
            .Select(procedure => new PatientProcedureResponse
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
                    .Select(x => new PatientProcedureStageResponse
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
                Assignments = procedure.Assignments
                    .Select(x => new PatientProcedureAssignmentResponse
                    {
                        PatientProcedureId = x.PatientProcedureId,
                        UserId = x.UserId,
                        AssignedByUserId = x.AssignedByUserId,
                        AssignedAtUtc = x.AssignedAtUtc
                    })
                    .ToList()
            })
            .ToList();
    }
}