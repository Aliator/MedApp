namespace MedApp.Contracts.Authentication.Requests;

/// <summary>Confirms an email address, with the account and token from the link sent to it.</summary>
public sealed record ConfirmEmailRequest(
    Guid UserId,
    string Token);
