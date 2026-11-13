using MedApp.Application.Procedures.Repositories;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.DeletePatientProcedure;

public sealed class DeletePatientProcedureHandler(IPatientProcedureRepository repository)
    : IRequestHandler<DeletePatientProcedureCommand, bool>
{
    public async Task<bool> Handle(DeletePatientProcedureCommand request, CancellationToken cancellationToken)
    {
        var procedure = await repository.GetByIdAsync(request.PatientProcedureId, cancellationToken);

        if (procedure is null)
        {
            return false;
        }

        await repository.DeleteAsync(request.PatientProcedureId, cancellationToken);

        return true;
    }
}