using MedApp.Application.Authentication.Emails.Common;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Emails.Commands.ChangeMyEmail;

/// <summary>
/// Makes the address pending and sends it a link to confirm it (valid for 24 hours). Asking for the address already
/// confirmed just drops any pending one. Refused (409) where email can't be sent, since the address could never be
/// confirmed.
/// </summary>
public sealed class ChangeMyEmailHandler(
    IAccountEmailService emails,
    EmailAvailability availability,
    IEmailSender sender,
    EmailLinks links,
    ISecurityEventLog events)
    : IRequestHandler<ChangeMyEmailCommand, EmailResponse>
{
    public async Task<EmailResponse> Handle(ChangeMyEmailCommand request, CancellationToken ct)
    {
        if (!availability.CanSendLinks)
            throw new ConflictException("MedApp can't send email here, so it can't confirm an email address.");

        var change = await emails.RequestChangeAsync(request.UserId, request.Email.Trim(), ct)
                     ?? throw new NotFoundException("Account not found.");

        if (change.Token is { } token && links.ConfirmEmail(request.UserId, token) is { } link)
        {
            await sender.SendAsync(EmailTemplates.ConfirmEmail(change.Email, change.Username, link), ct);
            await events.RecordAsync(
                new SecurityEvent(SecurityEventKind.EmailChangeRequested, request.UserId, change.Username),
                ct);
        }

        var account = await emails.GetAsync(request.UserId, ct)
                      ?? throw new NotFoundException("Account not found.");
        return EmailMapper.ToResponse(account);
    }
}
