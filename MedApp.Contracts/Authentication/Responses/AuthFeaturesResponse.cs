namespace MedApp.Contracts.Authentication.Responses;

/// <summary>
/// What this deployment's sign-in offers, for pages shown before signing in, so none offers what isn't there.
/// </summary>
/// <param name="CanSendEmail">
/// Whether it sends email with links (a mail server and its own address are set): without it there's no "Forgot your
/// password?" and no email addresses to confirm.
/// </param>
public sealed record AuthFeaturesResponse(
    bool CanSendEmail);
