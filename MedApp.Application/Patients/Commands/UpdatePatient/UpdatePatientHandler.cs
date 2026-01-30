using MediatR;
using MedApp.Application.Patients.Repositories;

namespace MedApp.Application.Patients.Commands.UpdatePatient;

public sealed class UpdatePatientHandler(IPatientRepository repository) : IRequestHandler<UpdatePatientCommand>
{
    public async Task Handle(UpdatePatientCommand request, CancellationToken cancellationToken)
    {
        var patient = await repository.GetByIdAsync(request.Id, cancellationToken);

        if (patient is null)
        {
            throw new KeyNotFoundException();
        }

        patient.FirstName = request.FirstName;
        patient.LastName = request.LastName;
        patient.Email = request.Email;
        patient.LastUpdated = DateTime.UtcNow;

        await repository.UpdateAsync(patient, cancellationToken);
    }
}