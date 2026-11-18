using MedApp.Application.Patients.Common;
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
    Guid? PracticeId = null,
    // None, Text, Email or TextAndEmail; null is None.
    string? ReminderChannel = null,
    // A GP practice not in the directory, held on this patient only; not with PracticeId.
    UnlistedPracticeInput? UnlistedPractice = null
) : IRequest<PatientResponse>;
