using MediatR;
using MedApp.Domain.Dtos.Responses;

namespace MedApp.Application.Patients.Commands.CreatePatient;

public sealed record CreatePatientCommand(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? Email
) : IRequest<PatientResponse>;