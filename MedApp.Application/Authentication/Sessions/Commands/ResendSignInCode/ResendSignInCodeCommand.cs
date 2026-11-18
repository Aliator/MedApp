using System.Text;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.ResendSignInCode;

/// <summary>
/// Emails a new code for a sign-in's second step, for an account using codes by email: the challenge the password step
/// returned. The new code replaces the last.
/// </summary>
public sealed record ResendSignInCodeCommand(
    string Challenge
) : IRequest<EmailCodeSentResponse>
{
    // Never prints the challenge.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Challenge)} = ***");
        return true;
    }
}
