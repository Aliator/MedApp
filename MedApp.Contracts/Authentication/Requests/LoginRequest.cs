namespace MedApp.Contracts.Authentication.Requests;

public sealed record LoginRequest(
    string Username,
    string Password);
