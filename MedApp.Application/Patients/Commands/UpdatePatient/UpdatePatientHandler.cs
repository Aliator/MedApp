using MediatR;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Patients.Responses;

namespace MedApp.Application.Patients.Commands.UpdatePatient;

public sealed class UpdatePatientHandler(IPatientRepository repository)
    : IRequestHandler<UpdatePatientCommand, PatientResponse?>
{
    public async Task<PatientResponse?> Handle(
        UpdatePatientCommand request,
        CancellationToken cancellationToken)
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

        return new PatientResponse(
            patient.Id,
            patient.FirstName,
            patient.LastName,
            patient.DateOfBirth,
            patient.Email
        );
    }
}