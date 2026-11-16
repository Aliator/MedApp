namespace MedApp.Contracts.Authentication.Requests;

/// <summary>A code from the signed-in account's authenticator app, to confirm a two-factor change.</summary>
public sealed record TwoFactorCodeRequest(
    string Code);
