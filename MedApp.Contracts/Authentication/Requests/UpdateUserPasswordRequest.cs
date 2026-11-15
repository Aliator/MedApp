namespace MedApp.Contracts.Authentication.Requests;

public sealed record UpdateUserPasswordRequest(
    string OldPassword,
    string NewPassword
);