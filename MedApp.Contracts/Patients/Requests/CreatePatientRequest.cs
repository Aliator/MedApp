namespace MedApp.Contracts.Patients.Requests;

public sealed record CreatePatientRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? Email);