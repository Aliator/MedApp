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

        if (request.FirstName is not null)
            patient.FirstName = request.FirstName;

        if (request.LastName is not null)
            patient.LastName = request.LastName;

        if (request.DateOfBirth.HasValue)
            patient.DateOfBirth = request.DateOfBirth.Value;

        if (request.Email is not null)
            patient.Email = request.Email;

        patient.LastUpdated = DateTime.UtcNow;

        await repository.UpdateAsync(patient, cancellationToken);

        return patient;
    }
}