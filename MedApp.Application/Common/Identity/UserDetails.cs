namespace MedApp.Application.Common.Identity;

public sealed record UserDetails(
    string Username,
    IReadOnlyList<string> Roles
);