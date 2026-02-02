using MedApp.Contracts.Patients.Responses;
using MediatR;

namespace MedApp.Application.Patients.Commands.CreatePatient;

public sealed record CreatePatientCommand(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? Email
) : IRequest<PatientResponse>;
