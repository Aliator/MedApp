using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.DeletePatientProcedure;

public sealed class DeletePatientProcedureHandler(
    IPatientProcedureRepository repository,
    IAttachmentRepository attachments,
    TimeProvider timeProvider)
    : IRequestHandler<DeletePatientProcedureCommand>
{
    public async Task Handle(DeletePatientProcedureCommand request, CancellationToken cancellationToken)
    {
        var procedure = await repository.GetForUpdateOrThrowAsync(request.Id, cancellationToken);

        if (!ProcedureWorkflow.CanBeDeleted(procedure))
            throw new ConflictException("Only cancelled or archived procedures can be deleted. Cancel it first.");

        // Its files (and its stages') would be left pointing at nothing: they must be deleted first, which is audited.
        var fileCount = await attachments.CountForProcedureAsync(procedure.Id, cancellationToken);
        if (fileCount > 0)
            throw new ConflictException(
                $"This procedure has {fileCount} file{(fileCount == 1 ? "" : "s")} attached, so it can't be deleted. Delete the files first.");

        var change = new ProcedureChange(procedure, request.Actor, timeProvider.GetUtcNow().UtcDateTime);
        change.Audit(PatientProcedureAuditAction.Deleted, new { status = procedure.Status.ToString() });

        await repository.DeleteAsync(procedure, change.AuditEntries, cancellationToken);
    }
}
