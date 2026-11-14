using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;

public sealed class CreatePatientProcedureHandler(
    IPatientProcedureRepository repository,
    IPatientRepository patientRepository,
    IPatientProcedureStagesRepository stagesRepository,
    TimeProvider timeProvider)
    : IRequestHandler<CreatePatientProcedureCommand, PatientProcedureResponse>
{
    public async Task<PatientProcedureResponse> Handle(CreatePatientProcedureCommand request, CancellationToken cancellationToken)
    {
        var patient = await patientRepository.GetByIdAsync(request.PatientId, cancellationToken)
                      ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        var template = await stagesRepository.GetStageTemplateByIdAsync(request.TemplateId, cancellationToken)
                       ?? throw new NotFoundException($"Template '{request.TemplateId}' was not found.");

        if (template.IsArchived)
            throw new ConflictException($"The '{template.Name}' template is archived, so it can't be used for new procedures.");
        if (template.Maps.Count == 0)
            throw new ConflictException($"The '{template.Name}' template has no stages.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var procedureId = Guid.NewGuid();

        var procedure = new PatientProcedure
        {
            Id = procedureId,
            PatientId = patient.Id,
            TemplateId = template.Id,
            TemplateName = template.Name,
            Title = string.IsNullOrWhiteSpace(request.Title) ? template.Name : request.Title.Trim(),
            Notes = request.Notes,
            DueDateUtc = DueDates.EndOfDayUtc(request.DueDate),
            Priority = ProcedureEnums.Parse<PatientProcedurePriority>(request.Priority),
            Status = PatientProcedureStatus.Unassigned,
            CreatedAt = now,
            LastUpdated = now,
            // A copy of the template's stages, so later edits to the template or definitions don't change this record.
            Stages = template.Maps
                .OrderBy(m => m.StageOrder)
                .Select((map, index) => new PatientProcedureStage
                {
                    Id = Guid.NewGuid(),
                    PatientProcedureId = procedureId,
                    StageDefinitionId = map.StageDefinitionId,
                    StageOrder = index + 1,
                    Name = map.StageDefinition.Name,
                    Description = map.StageDefinition.Description,
                    Instructions = map.StageDefinition.Instructions,
                    CreatedAt = now,
                    LastUpdated = now
                })
                .ToList()
        };

        var change = new ProcedureChange(procedure, request.Actor, now);
        change.Audit(PatientProcedureAuditAction.Created, new
        {
            template = template.Name,
            dueDate = request.DueDate,
            priority = procedure.Priority.ToString(),
            stageCount = procedure.Stages.Count
        });

        await repository.AddAsync(procedure, change.AuditEntries, cancellationToken);

        return ProcedureMapper.ToResponse(procedure, new Dictionary<Guid, string>(), now, ProcedureMapper.PatientName(patient));
    }
}
