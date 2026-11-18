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
    /// last, or after <see cref="EmailCode.MaxResends"/>. A code that has used up its tries isn't live, so asking again
    /// starts a fresh one, with fresh tries. Each email counts towards the account's limit
    /// (<see cref="AccountAttempt.EmailCode"/>). Null, with nothing sent or stored, when the account has no confirmed
    /// address or the email can't go now (no mail server, or the hour's allowance for codes used up), so nobody is told a
    /// code is on its way when it isn't.
    /// </summary>
    /// <param name="notAfterUtc">When what the code is for ends (a sign-in's challenge): the code ends then too.</param>
    Task<EmailCodeSent?> SendAsync(
        Guid userId,
        EmailCodePurpose purpose,
        Guid? challengeId,
        CancellationToken ct,
        DateTime? notAfterUtc = null);

    /// <summary>
    /// Whether <paramref name="code"/> is the account's live code for <paramref name="purpose"/> (for sign-in, for
    /// <paramref name="challengeId"/>). A right one is used up, so it works once. For turning on and confirming, each try
    /// first reserves one of the code's tries (<see cref="EmailCode.MaxFailedAttempts"/>); a sign-in's are its
    /// challenge's. <see cref="EmailCodeCheck.Wrong"/> is a guess, which the caller counts as it would a wrong
    /// authenticator code; <see cref="EmailCodeCheck.NotLive"/> (no live code, no tries left, or used meanwhile) can never
    /// succeed, so it isn't counted as one.
    /// </summary>
    Task<EmailCodeCheck> CheckAsync(Guid userId, EmailCodePurpose purpose, Guid? challengeId, string code, CancellationToken ct);
}

/// <summary>What checking an emailed code found.</summary>
public enum EmailCodeCheck
{
    /// <summary>A live code, but not this one: a wrong guess. The default, so nothing is accepted by accident.</summary>
    Wrong = 0,

    /// <summary>The right code: it's been used up.</summary>
    Right = 1,

    /// <summary>No code that could work: none sent, run out, out of tries, or already used.</summary>
    NotLive = 2,
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
    public async Task<EmailCodeSent?> SendAsync(
        Guid userId,
        EmailCodePurpose purpose,
        Guid? challengeId,
        CancellationToken ct,
        DateTime? notAfterUtc = null)
    {
        // Only ever the confirmed address: a pending one is used once its link has been opened.
        var address = (await emails.GetAsync(userId, ct))?.Email;
        if (string.IsNullOrWhiteSpace(address) || !sender.HasRoomFor(EmailCategory.Code))
            return null;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var live = await codes.FindLiveAsync(userId, purpose, challengeId, now, ct);
        if (live is not null)
            ThrowIfCantResend(live, now);

        attempts.Attempt(AccountAttempt.EmailCode, userId);

        var code = EmailCodes.Generate();
        var boundTo = BoundTo(purpose, address);
        EmailCode sent;
        if (live is null)
        {
            sent = new EmailCode(userId, purpose, challengeId, now, notAfterUtc);
            sent.Issue(EmailCodes.Hash(sent.Id, code, boundTo));
            // Refused when another request made a live code at the same moment: only that one is sent.
            if (!await codes.AddAsync(sent, now, ct))
                throw TooSoon(now.Add(EmailCode.ResendInterval), now);
        }
        else
        {
            // One statement decides, so of two resends at once only one sends.
            var expires = EmailCode.ExpiryFor(now, notAfterUtc);
            if (!await codes.TryResendAsync(live.Id, EmailCodes.Hash(live.Id, code, boundTo), now, expires, ct))
                throw TooSoon(now.Add(EmailCode.ResendInterval), now);
            sent = await codes.FindLiveAsync(userId, purpose, challengeId, now, ct) ?? live;
        }

        // The email says how long this code works, which for a sign-in may be less than the usual 10 minutes.
        await sender.SendAsync(
            EmailTemplates.TwoFactorCode(address, code, purpose == EmailCodePurpose.SignIn, sent.ExpiresAtUtc - now),
            ct);

        return new EmailCodeSent(EmailAddressMask.Mask(address), sent.ExpiresAtUtc, sent.ResendAvailableAtUtc, sent.ResendsLeft);
    }

    public async Task<EmailCodeCheck> CheckAsync(
        Guid userId,
        EmailCodePurpose purpose,
        Guid? challengeId,
        string code,
        CancellationToken ct)
    {
        if (!EmailCodes.IsWellFormed(code))
            return EmailCodeCheck.Wrong;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var stored = await codes.FindLiveAsync(userId, purpose, challengeId, now, ct);
        if (stored is null)
            return EmailCodeCheck.NotLive;

        // A code for turning codes on works only while the address it went to is still the confirmed one.
        string? boundTo = null;
        if (purpose == EmailCodePurpose.TurnOn)
        {
            var address = (await emails.GetAsync(userId, ct))?.Email;
            if (string.IsNullOrWhiteSpace(address))
                return EmailCodeCheck.NotLive;
            boundTo = address;
        }

        // A sign-in's tries are reserved on its challenge; the others' on the code itself.
        if (purpose != EmailCodePurpose.SignIn && !await codes.TryReserveAttemptAsync(stored.Id, now, ct))
            return EmailCodeCheck.NotLive;

        if (!EmailCodes.Matches(stored, code, boundTo))
            return EmailCodeCheck.Wrong;

        // Right, but used by another request first (or replaced meanwhile): not a guess.
        return await codes.ConsumeAsync(stored.Id, stored.CodeHash, ct) ? EmailCodeCheck.Right : EmailCodeCheck.NotLive;
    }

    /// <summary>What a code's hash is bound to: the address, for a code that proves the mailbox when turning codes on.</summary>
    private static string? BoundTo(EmailCodePurpose purpose, string address) =>
        purpose == EmailCodePurpose.TurnOn ? address : null;

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
