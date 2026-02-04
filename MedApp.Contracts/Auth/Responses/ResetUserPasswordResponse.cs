using Microsoft.AspNetCore.Identity;

namespace MedApp.Contracts.Auth.Responses;

public sealed record ResetUserPasswordResponse(
    string? GeneratedPassword,
    IEnumerable<IdentityError> Errors);