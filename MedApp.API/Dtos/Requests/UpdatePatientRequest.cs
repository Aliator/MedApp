namespace MedApp.API.Dtos.Requests;

public sealed record UpdatePatientRequest(
    string? FirstName,
    string? LastName,
    DateOnly? DateOfBirth,
    string? Email);