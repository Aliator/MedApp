using MedApp.Contracts.Patients.Responses;
using MediatR;

namespace MedApp.Application.Patients.Commands.UpdatePatient;

public sealed record UpdatePatientCommand(
    Guid Id,
    string? FirstName,
    string? LastName,
    DateOnly? DateOfBirth,
    string? Email,
    // Null leaves it as it is; empty (or spaces) removes it.
    string? NhsNumber = null,
    // Null leaves them as they are; an empty list removes them.
    IReadOnlyList<string>? AddressLines = null,
    // Null leaves it as it is; empty (or spaces) removes it.
    string? Postcode = null,
    // Null leaves it as it is; empty (or spaces) removes it.
    string? MobilePhone = null,
    // Null leaves it as it is; Guid.Empty removes it.
    Guid? PracticeId = null
) : IRequest<PatientResponse?>;