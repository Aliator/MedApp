namespace MedApp.Contracts.Authentication.Requests;

/// <summary>A new email address for the signed-in account. It's used once the link sent to it is opened.</summary>
public sealed record ChangeEmailRequest(
    string Email);
