using MedApp.Application.Patients.Common;
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
    Guid? PracticeId = null,
    // None, Text, Email or TextAndEmail; null leaves it as it is.
    string? ReminderChannel = null,
    // A GP practice not in the directory, held on this patient only: null leaves it as it is; given, it replaces a
    // directory practice. Not with a PracticeId other than Guid.Empty (which removes both).
    UnlistedPracticeInput? UnlistedPractice = null
) : IRequest<PatientResponse?>;