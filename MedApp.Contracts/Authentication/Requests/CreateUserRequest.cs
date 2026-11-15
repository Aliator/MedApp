namespace MedApp.Contracts.Authentication.Requests;

public sealed record CreateUserRequest(
    string Username,
    string Password);
