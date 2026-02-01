namespace MedApp.Domain.Dtos.Requests;

public sealed record LoginRequest(
    string Username,
    string Password);
