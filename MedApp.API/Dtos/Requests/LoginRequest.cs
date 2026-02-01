namespace MedApp.API.Dtos.Requests;

public sealed record LoginRequest(
    string Username,
    string Password);
