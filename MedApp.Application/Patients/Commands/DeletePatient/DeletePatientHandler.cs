using MediatR;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;

namespace MedApp.Application.Patients.Commands.DeletePatient;

public sealed class DeletePatientHandler(IPatientRepository repository, IAttachmentRepository attachments)
    : IRequestHandler<DeletePatientCommand, bool>
{
    public async Task<bool> Handle(DeletePatientCommand request, CancellationToken cancellationToken)
    {
        var patient = await repository.GetByIdAsync(request.Id, cancellationToken);

        if (patient is null)
            return false;

        // Procedures keep a patient's medical history, so a patient who has any can't be deleted (409).
        var procedureCount = await repository.CountProceduresAsync(request.Id, cancellationToken);
        if (procedureCount > 0)
            throw new ConflictException(
                $"This patient has {procedureCount} procedure{(procedureCount == 1 ? "" : "s")}, so they can't be deleted.");

        // So are the files attached to them: they must be deleted first (which is audited).
        var fileCount = await attachments.CountForPatientAsync(request.Id, cancellationToken);
        if (fileCount > 0)
            throw new ConflictException(
                $"This patient has {fileCount} file{(fileCount == 1 ? "" : "s")} attached, so they can't be deleted. Delete the files first.");

        await repository.DeleteAsync(request.Id, cancellationToken);

        return true;
    }
}