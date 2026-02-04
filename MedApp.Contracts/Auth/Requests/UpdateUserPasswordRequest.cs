namespace MedApp.Contracts.Auth.Requests;

public sealed record UpdateUserPasswordRequest(
    string OldPassword,
    string NewPassword
);