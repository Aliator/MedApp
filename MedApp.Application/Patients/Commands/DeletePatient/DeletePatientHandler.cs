using MediatR;
using MedApp.Application.Patients.Repositories;

namespace MedApp.Application.Patients.Commands.DeletePatient;

public sealed class DeletePatientHandler(IPatientRepository repository) : IRequestHandler<DeletePatientCommand>
{
    public async Task Handle(DeletePatientCommand request, CancellationToken cancellationToken)
    {
        await repository.DeleteAsync(request.Id, cancellationToken);
    }
}