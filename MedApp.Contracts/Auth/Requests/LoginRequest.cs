namespace MedApp.Contracts.Auth.Requests;

public sealed record LoginRequest(
    string Username,
    string Password);
