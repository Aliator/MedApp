using MediatR;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Patients;

namespace MedApp.Application.Patients.Commands.UpdatePatient;

public sealed class UpdatePatientHandler(IPatientRepository repository)
    : IRequestHandler<UpdatePatientCommand, Patient?>
{
    public async Task<Patient?> Handle(UpdatePatientCommand request, CancellationToken cancellationToken)
    {
        var patient = await repository.GetByIdAsync(request.Id, cancellationToken);

        if (patient is null)
            return null;

        patient.FirstName = request.FirstName;
        patient.LastName = request.LastName;
        patient.Email = request.Email;
        patient.DateOfBirth = request.DateOfBirth;
        patient.LastUpdated = DateTime.UtcNow;

        await repository.UpdateAsync(patient, cancellationToken);

        return patient;
    }
}