using MediatR;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Patients;

namespace MedApp.Application.Patients.Commands.CreatePatient;

public sealed class CreatePatientHandler(IPatientRepository repository)
    : IRequestHandler<CreatePatientCommand, PatientResponse>
{
    public async Task<PatientResponse> Handle(
        CreatePatientCommand request,
        CancellationToken cancellationToken)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        await repository.AddAsync(patient, cancellationToken);

        return new PatientResponse(
            patient.Id,
            patient.FirstName,
            patient.LastName,
            patient.DateOfBirth,
            patient.Email
        );
    }
}