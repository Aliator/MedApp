using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.CreatePatientPathway;

public sealed class CreatePatientPathwayHandler(
    IPatientPathwayRepository repository,
    IPatientRepository patientRepository,
    IPatientPathwayStagesRepository stagesRepository,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<CreatePatientPathwayCommand, PatientPathwayResponse>
{
    public async Task<PatientPathwayResponse> Handle(CreatePatientPathwayCommand request, CancellationToken cancellationToken)
    {
        var patient = await patientRepository.GetByIdAsync(request.PatientId, cancellationToken)
                      ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        // A restricted or anonymised record can't gain new pathways (409).
        PatientRules.EnsureCanChange(patient);

        var template = await stagesRepository.GetPathwayTemplateByIdAsync(request.TemplateId, cancellationToken)
                       ?? throw new NotFoundException($"Template '{request.TemplateId}' was not found.");

        if (template.IsArchived)
            throw new ConflictException($"The '{template.Name}' template is archived, so it can't be used for new pathways.");
        if (template.Maps.Count == 0)
            throw new ConflictException($"The '{template.Name}' template has no stages.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var pathway = PathwayCreation.FromTemplate(
            template,
            patient.Id,
            request.Title,
            request.DueDate,
            PathwayEnums.Parse<PatientPathwayPriority>(request.Priority),
            now);

        var change = new PathwayChange(pathway, request.Actor, now);
        PathwayCreation.Record(change, template.Name, request.DueDate, request.Notes);

        await repository.AddAsync(change, cancellationToken);
        await pathwayMonitor.NotifyAsync(change, cancellationToken);

        // The creator's own note, if any, is the only one, so the actor is the only username it names.
        var usernames = new Dictionary<Guid, string> { [request.Actor.UserId] = request.Actor.Username };
        return PathwayMapper.ToResponse(pathway, usernames, now, patient: patient, viewerId: request.Actor.UserId, viewerCanCorrectRecords: request.Actor.Has(Permission.CorrectRecords));
    }
}
