namespace MedApp.Domain.Dtos.Requests;

public sealed record CreateUserRequest(
    string Username,
    string Password);
