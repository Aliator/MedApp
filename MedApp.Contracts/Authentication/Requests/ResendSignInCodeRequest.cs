namespace MedApp.Contracts.Authentication.Requests;

/// <summary>Asks for a new emailed code for a sign-in's second step: the challenge the password step returned.</summary>
public sealed record ResendSignInCodeRequest(string Challenge);
