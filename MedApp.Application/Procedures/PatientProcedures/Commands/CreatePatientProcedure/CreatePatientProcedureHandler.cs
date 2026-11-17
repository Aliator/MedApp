using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Patients.Common;
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
    IProcedureMonitor procedureMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<CreatePatientProcedureCommand, PatientProcedureResponse>
{
    public async Task<PatientProcedureResponse> Handle(CreatePatientProcedureCommand request, CancellationToken cancellationToken)
    {
        var patient = await patientRepository.GetByIdAsync(request.PatientId, cancellationToken)
                      ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        // A restricted or anonymised record can't gain new procedures (409).
        PatientRules.EnsureCanChange(patient);

        var template = await stagesRepository.GetStageTemplateByIdAsync(request.TemplateId, cancellationToken)
                       ?? throw new NotFoundException($"Template '{request.TemplateId}' was not found.");

        if (template.IsArchived)
            throw new ConflictException($"The '{template.Name}' template is archived, so it can't be used for new procedures.");
        if (template.Maps.Count == 0)
            throw new ConflictException($"The '{template.Name}' template has no stages.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var procedure = ProcedureCreation.FromTemplate(
            template,
            patient.Id,
            request.Title,
            request.Notes,
            request.DueDate,
            ProcedureEnums.Parse<PatientProcedurePriority>(request.Priority),
            now);

        var change = new ProcedureChange(procedure, request.Actor, now);
        ProcedureCreation.Record(change, template.Name, request.DueDate);

        await repository.AddAsync(change, cancellationToken);
        await procedureMonitor.NotifyAsync(change, cancellationToken);

        return ProcedureMapper.ToResponse(procedure, new Dictionary<Guid, string>(), now, patient: patient);
    }
}
