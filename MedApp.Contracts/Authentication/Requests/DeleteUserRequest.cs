namespace MedApp.Contracts.Authentication.Requests;

/// <summary>Deleting a user: the signed-in admin's own password, to confirm it's them and that they mean it.</summary>
public sealed record DeleteUserRequest(
    string? Password);
