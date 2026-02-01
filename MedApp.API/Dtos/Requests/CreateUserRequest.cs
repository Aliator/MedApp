namespace MedApp.API.Dtos.Requests;

public sealed record CreateUserRequest(
    string Username,
    string Password);
