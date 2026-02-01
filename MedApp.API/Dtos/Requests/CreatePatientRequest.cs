namespace MedApp.API.Dtos.Requests;

public sealed record CreatePatientRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? Email);