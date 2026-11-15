using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

/// <summary>
/// The outcome of creating an account: Identity's result and, when it succeeded, the one-time password the server
/// generated for it. The password isn't stored anywhere in plain text, so this is the only time it can be shown.
/// </summary>
public sealed record CreateUserResult(
    IdentityResult Result,
    string? GeneratedPassword);