using MedApp.Domain.Patients;
using MediatR;

namespace MedApp.Application.Patients.Commands.UpdatePatient;

public sealed record UpdatePatientCommand(
    Guid Id,
    string FirstName,
    string LastName,
    DateTime DateOfBirth,
    string? Email
) : IRequest<Patient>;
