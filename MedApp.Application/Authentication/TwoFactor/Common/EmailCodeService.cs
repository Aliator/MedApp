using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Domain.Authentication;

namespace MedApp.Application.Authentication.TwoFactor.Common;

/// <summary>
/// Two-factor codes by email: sending one to the account's confirmed address (a new code, or the same purpose's code
/// sent again as a new one), and checking one. See <see cref="EmailCode"/> for the rules and <see cref="EmailCodes"/> for
/// how they're made and stored.
/// </summary>
public interface IEmailCodeService
{
    /// <summary>
    /// Emails a code for <paramref name="purpose"/> (for sign-in, for <paramref name="challengeId"/>) to the account's
    /// confirmed address. If the account already has a live code for it, this sends it again, as a new code that replaces
    /// it: refused (<see cref="TooManyAttemptsException"/>, a 429) within <see cref="EmailCode.ResendInterval"/> of the
    /// last, or after <see cref="EmailCode.MaxResends"/>. Each email counts towards the account's limit
    /// (<see cref="AccountAttempt.EmailCode"/>). Null, with nothing sent, when the account has no confirmed address.
    /// </summary>
    Task<EmailCodeSent?> SendAsync(Guid userId, EmailCodePurpose purpose, Guid? challengeId, CancellationToken ct);

    /// <summary>
    /// Whether <paramref name="code"/> is the account's live code for <paramref name="purpose"/> (for sign-in, for
    /// <paramref name="challengeId"/>). A right one is used up, so it works once. For turning on and confirming, each try
    /// first reserves one of the code's attempts (<see cref="EmailCode.MaxFailedAttempts"/>); a sign-in's are its
    /// challenge's. The caller counts a wrong one as it would a wrong authenticator code.
    /// </summary>
    Task<bool> CheckAsync(Guid userId, EmailCodePurpose purpose, Guid? challengeId, string code, CancellationToken ct);
}

/// <summary>A code was emailed: where to (masked), when it stops working, and when and how often it can be sent again.</summary>
public sealed record EmailCodeSent(string MaskedAddress, DateTime ExpiresAtUtc, DateTime ResendAvailableAtUtc, int ResendsLeft);

public sealed class EmailCodeService(
    IEmailCodeRepository codes,
    IAccountEmailService emails,
    IEmailSender sender,
    IAccountAttemptLimiter attempts,
    TimeProvider timeProvider)
    : IEmailCodeService
{
    public async Task<EmailCodeSent?> SendAsync(Guid userId, EmailCodePurpose purpose, Guid? challengeId, CancellationToken ct)
    {
        // Only ever the confirmed address: a pending one is used once its link has been opened.
        var address = (await emails.GetAsync(userId, ct))?.Email;
        if (string.IsNullOrWhiteSpace(address))
            return null;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var live = await codes.FindLiveAsync(userId, purpose, challengeId, now, ct);
        if (live is not null)
            ThrowIfCantResend(live, now);

        attempts.Attempt(AccountAttempt.EmailCode, userId);

        var code = EmailCodes.Generate();
        EmailCode sent;
        if (live is null)
        {
            sent = new EmailCode(userId, purpose, challengeId, now);
            sent.Issue(EmailCodes.Hash(sent.Id, code));
            await codes.AddAsync(sent, now, ct);
        }
        else
        {
            // One statement decides, so of two resends at once only one sends.
            if (!await codes.TryResendAsync(live.Id, EmailCodes.Hash(live.Id, code), now, ct))
                throw TooSoon(now.Add(EmailCode.ResendInterval), now);
            sent = await codes.FindLiveAsync(userId, purpose, challengeId, now, ct) ?? live;
        }

        await sender.SendAsync(
            EmailTemplates.TwoFactorCode(address, code, purpose == EmailCodePurpose.SignIn, EmailCode.Lifetime),
            ct);

        return new EmailCodeSent(EmailAddressMask.Mask(address), sent.ExpiresAtUtc, sent.ResendAvailableAtUtc, sent.ResendsLeft);
    }

    public async Task<bool> CheckAsync(Guid userId, EmailCodePurpose purpose, Guid? challengeId, string code, CancellationToken ct)
    {
        if (!EmailCodes.IsWellFormed(code))
            return false;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var stored = await codes.FindLiveAsync(userId, purpose, challengeId, now, ct);
        if (stored is null)
            return false;

        // A sign-in's attempts are reserved on its challenge; the others' on the code itself.
        if (purpose != EmailCodePurpose.SignIn && !await codes.TryReserveAttemptAsync(stored.Id, now, ct))
            return false;

        return EmailCodes.Matches(stored, code)
               && await codes.ConsumeAsync(stored.Id, stored.CodeHash, ct);
    }

    private static void ThrowIfCantResend(EmailCode live, DateTime now)
    {
        if (live.ResendsLeft == 0)
        {
            var seconds = (int)Math.Ceiling((live.ExpiresAtUtc - now).TotalSeconds);
            throw new TooManyAttemptsException(
                "That code has been sent as many times as it can be. Use the newest one, or start again once it has run out.",
                seconds);
        }

        if (now < live.ResendAvailableAtUtc)
            throw TooSoon(live.ResendAvailableAtUtc, now);
    }

    private static TooManyAttemptsException TooSoon(DateTime availableAtUtc, DateTime now)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling((availableAtUtc - now).TotalSeconds));
        return new TooManyAttemptsException(
            $"A code was sent less than a minute ago. Wait {seconds} {(seconds == 1 ? "second" : "seconds")}, then ask for another.",
            seconds);
    }
}
