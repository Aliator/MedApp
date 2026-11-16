using MediatR;

namespace MedApp.Application.Authentication.Passwords.Commands.ForgotPassword;

/// <summary>
/// Asks for a password reset link for a username or a confirmed email address. The answer never says whether an account
/// matched: the API gives the same one either way.
/// </summary>
public sealed record ForgotPasswordCommand(
    string UsernameOrEmail
) : IRequest;
