namespace MedApp.Contracts.Authentication.Responses;

/// <summary>The signed-in account's email address.</summary>
/// <param name="Email">The confirmed address MedApp sends to; null if none.</param>
/// <param name="PendingEmail">An address waiting for its link to be opened; null if none.</param>
public sealed record EmailResponse(
    string? Email,
    string? PendingEmail);
