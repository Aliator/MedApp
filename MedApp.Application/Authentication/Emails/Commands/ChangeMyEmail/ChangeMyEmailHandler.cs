using MedApp.Application.Authentication.Emails.Common;
using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;
using MediatR;

namespace MedApp.Application.Authentication.Emails.Commands.ChangeMyEmail;

/// <summary>
/// Checks the password (a session alone can't change where reset links go), counts the request against the account's
/// limit (each sends an email), stores the bare address as pending, and sends it a link to confirm it (valid for 24
/// hours). The confirmed address, if there is one, is told straight away, so a change its owner didn't ask for is seen
/// before it takes effect. Asking for the address already confirmed just drops any pending one. Refused (409) where email
/// can't be sent, since the address could never be confirmed. While the account signs in with codes by email, it needs
/// a code too (<see cref="ChangeMyEmailCommand.Code"/>: an emailed one or a recovery code), counted as any code is.
/// </summary>
public sealed class ChangeMyEmailHandler(
    IAccountEmailService emails,
    IIdentityUserService users,
    ITwoFactorService twoFactor,
    EmailAvailability availability,
    IEmailSender sender,
    EmailLinks links,
    ISecurityNotices notices,
    ISecurityEventLog events,
    IAccountAttemptLimiter attempts,
    IEmailCodeService emailCodes)
    : IRequestHandler<ChangeMyEmailCommand, EmailResponse>
{
    public const string CodeNeeded =
        "You sign in with codes sent to your email address, so enter a code we email you (or a recovery code) to change it.";

    public async Task<EmailResponse> Handle(ChangeMyEmailCommand request, CancellationToken ct)
    {
        if (!availability.CanSendLinks)
            throw new ConflictException("MedApp can't send email here, so it can't confirm an email address.");

        var account = await emails.GetAsync(request.UserId, ct)
                      ?? throw new NotFoundException("Account not found.");

        attempts.Attempt(AccountAttempt.EmailChange, request.UserId);
        await users.VerifyOrThrowAsync(twoFactor, request.UserId, account.Username, request.Password, ct);

        // While the address is the second factor, moving it needs that factor too (an emailed code or a recovery code),
        // so a session and the password can't send the codes to someone else's mailbox.
        if (await twoFactor.GetMethodAsync(request.UserId, ct) == TwoFactorMethod.Email)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
                throw new FluentValidation.ValidationException(
                    [new FluentValidation.Results.ValidationFailure(nameof(request.Code), CodeNeeded)]);

            await twoFactor.VerifyOrThrowAsync(
                emailCodes, attempts, notices, request.UserId, TwoFactorMethod.Email, request.Code,
                nameof(request.Code), allowRecoveryCode: true, ct);
        }

        var address = EmailAddressRules.Normalize(request.Email)
                      ?? throw new FluentValidation.ValidationException(
                          [new FluentValidation.Results.ValidationFailure(nameof(request.Email), "Enter one email address, like name@example.org.")]);

        var change = await emails.RequestChangeAsync(request.UserId, address, ct)
                     ?? throw new NotFoundException("Account not found.");

        if (change.Token is { } token && links.ConfirmEmail(request.UserId, token) is { } link)
        {
            await sender.SendAsync(EmailTemplates.ConfirmAddress(change.Email, change.Username, link), ct);
            await events.RecordAsync(
                new SecurityEvent(SecurityEventKind.AddressChangeRequested, request.UserId, change.Username),
                ct);

            if (account.Email is { } current && !string.Equals(current, change.Email, StringComparison.OrdinalIgnoreCase))
                await notices.SendAsync(request.UserId, SecurityNotice.AddressChangeRequested, ct, toAddress: current);
        }

        var now = await emails.GetAsync(request.UserId, ct)
                  ?? throw new NotFoundException("Account not found.");
        return EmailMapper.ToResponse(now);
    }
}
