using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MedApp.Application.Common.Authentication;

/// <summary>
/// What a right password leads to: a session (<see cref="SignedIn"/>), or, for an account with two-factor
/// authentication, a challenge for the second step and no session yet (<see cref="TwoFactorRequired"/>).
/// </summary>
public sealed record LoginOutcome
{
    private LoginOutcome(LoginResult? session, string? challenge, DateTime? challengeExpiresAtUtc)
    {
        Session = session;
        Challenge = challenge;
        ChallengeExpiresAtUtc = challengeExpiresAtUtc;
    }

    /// <summary>The new session, when signing in is complete; otherwise null.</summary>
    public LoginResult? Session { get; }

    /// <summary>The second step's challenge token, when a code is needed; otherwise null.</summary>
    public string? Challenge { get; }

    public DateTime? ChallengeExpiresAtUtc { get; }

    [MemberNotNullWhen(true, nameof(Challenge), nameof(ChallengeExpiresAtUtc))]
    [MemberNotNullWhen(false, nameof(Session))]
    public bool NeedsTwoFactor => Challenge is not null;

    public static LoginOutcome SignedIn(LoginResult session) => new(session, null, null);

    public static LoginOutcome TwoFactorRequired(string challenge, DateTime expiresAtUtc) =>
        new(null, challenge, expiresAtUtc);

    // Never prints the challenge.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(NeedsTwoFactor)} = {NeedsTwoFactor}");
        return true;
    }
}
