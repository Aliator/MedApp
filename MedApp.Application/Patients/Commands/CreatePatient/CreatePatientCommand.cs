using MediatR;

namespace MedApp.Application.Patients.Commands.CreatePatient;

public sealed record CreatePatientCommand(
    string FirstName,
    string LastName,
    DateTime DateOfBirth,
    string? Email
) : IRequest<Guid>;