using MedApp.Application.Common.Email;
using MedApp.Application.Common.Identity;

namespace MedApp.Application.Authentication.TwoFactor.Common;

/// <summary>Whether an account could turn two-factor codes by email on: a confirmed address, and email this server can send.</summary>
public static class EmailCodesAvailability
{
    /// <summary>No confirmed address (none, or one still waiting for its link to be opened).</summary>
    public const string NoConfirmedAddress = "NoConfirmedAddress";

    /// <summary>This server has no mail server to send with.</summary>
    public const string EmailUnavailable = "EmailUnavailable";

    /// <summary>Why the account can't use codes by email (<see cref="NoConfirmedAddress"/> or <see cref="EmailUnavailable"/>), or null if it can.</summary>
    public static async Task<string?> WhyNotAsync(IAccountEmailService emails, IEmailSender sender, Guid userId, CancellationToken ct)
    {
        if (!sender.CanSend)
            return EmailUnavailable;

        return string.IsNullOrWhiteSpace((await emails.GetAsync(userId, ct))?.Email) ? NoConfirmedAddress : null;
    }

    /// <summary>The refusal for each reason, in words.</summary>
    public static string Explain(string reason) => reason switch
    {
        EmailUnavailable => "This server can't send email, so codes by email aren't available.",
        _ => "Confirm an email address for your account first (Account Settings, Email Address): codes are only ever sent to a confirmed one.",
    };
}
