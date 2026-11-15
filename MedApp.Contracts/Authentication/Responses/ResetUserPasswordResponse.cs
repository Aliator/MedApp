using Microsoft.AspNetCore.Identity;

namespace MedApp.Contracts.Authentication.Responses;

public sealed record ResetUserPasswordResponse(
    string? GeneratedPassword,
    IEnumerable<IdentityError> Errors);