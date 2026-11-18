using MedApp.Contracts.Patients.Responses;
using MediatR;

namespace MedApp.Application.Patients.Commands.CreatePatient;

public sealed record CreatePatientCommand(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Email,
    string? NhsNumber = null,
    IReadOnlyList<string>? AddressLines = null,
    string? Postcode = null,
    string? MobilePhone = null,
    Guid? PracticeId = null
) : IRequest<PatientResponse>;
