namespace MedApp.Application.Common.Email;

/// <summary>
/// Whether this deployment can send emails with links: a mail server (<see cref="IEmailSender.CanSend"/>) and the app's
/// own address for links (<see cref="EmailLinks.CanLink"/>). Without both, nothing offers what needs them: no "Forgot
/// your password?", and no email addresses to confirm.
/// </summary>
public sealed class EmailAvailability(IEmailSender sender, EmailLinks links)
{
    public bool CanSendLinks => sender.CanSend && links.CanLink;
}
