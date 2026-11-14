using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;

public sealed class UpdatePatientProcedureHandler(
    IPatientProcedureRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<UpdatePatientProcedureCommand, PatientProcedureResponse>
{
    public async Task<PatientProcedureResponse> Handle(UpdatePatientProcedureCommand request, CancellationToken cancellationToken)
    {
        var procedure = await repository.GetForUpdateOrThrowAsync(request.Id, cancellationToken);

        if (!ProcedureWorkflow.CanEditDetails(procedure))
            throw new ConflictException($"A procedure that's {ProcedureWorkflow.Describe(procedure.Status)} can't be edited.");

        // What changed, from and to, for the audit log.
        var changes = new Dictionary<string, object>();

        if (request.Title?.Trim() is { } title && title != procedure.Title)
        {
            changes["title"] = new { from = procedure.Title, to = title };
            procedure.Title = title;
        }

        if (request.Notes is { } notes && notes != procedure.Notes)
        {
            changes["notes"] = new { from = procedure.Notes, to = notes };
            procedure.Notes = notes;
        }

        if (request.DueDate is { } dueDate && dueDate != DueDates.ToUkDate(procedure.DueDateUtc))
        {
            changes["dueDate"] = new { from = DueDates.ToUkDate(procedure.DueDateUtc), to = dueDate };
            procedure.DueDateUtc = DueDates.EndOfDayUtc(dueDate);
        }

        if (request.Priority is not null
            && ProcedureEnums.Parse<PatientProcedurePriority>(request.Priority) is var priority
            && priority != procedure.Priority)
        {
            changes["priority"] = new { from = procedure.Priority.ToString(), to = priority.ToString() };
            procedure.Priority = priority;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (changes.Count > 0)
        {
            var change = new ProcedureChange(procedure, request.Actor, now);
            change.Audit(PatientProcedureAuditAction.DetailsUpdated, new { changes });
            await repository.SaveAsync(change.AuditEntries, cancellationToken);
        }

        return await ProcedureMapper.ToResponseAsync(procedure, identityReadService, now, cancellationToken);
    }
}
