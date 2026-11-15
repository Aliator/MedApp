namespace MedApp.Contracts.Patients.Responses;

public sealed record PatientResponse(
    Guid Id,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Email
);