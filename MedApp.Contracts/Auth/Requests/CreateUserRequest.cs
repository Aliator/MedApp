namespace MedApp.Contracts.Auth.Requests;

public sealed record CreateUserRequest(
    string Username,
    string Password);
