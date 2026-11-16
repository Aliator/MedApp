using MedApp.Application.Common.Email;
using MedApp.Application.Common.Identity;
using Microsoft.Extensions.Options;
using Moq;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic email addresses, links and two-factor values for the email, reset and two-factor tests.</summary>
public static class EmailTestConstants
{
    public const string LinkBaseUrl = "https://medapp.example.test";
    public const string Address = "synthetic.user@example.test";
    public const string OtherAddress = "other.address@example.test";
    public const string Token = "synthetic+token/with=chars";
    public const string Username = "synthetic.user";
    public const string NewPassword = "Synthetic-New-Passw0rd";
    public const string CurrentPassword = "Synthetic-Current-Passw0rd";
    public const string AuthenticatorCode = "123456";
    public const string RecoveryCode = "ABCDE-23456";
    public const string ChallengeToken = "synthetic-challenge-token";

    /// <summary>An authenticator key (Base32), as Identity stores them.</summary>
    public const string AuthenticatorKeyBase32 = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";

    public static readonly Guid UserId = Guid.Parse("0000000f-0000-0000-0000-000000000001");
    public static readonly Guid ActorId = Guid.Parse("0000000f-0000-0000-0000-000000000002");
    public static readonly Guid SessionId = Guid.Parse("0000000f-0000-0000-0000-000000000003");

    public static readonly DateTime NowUtc = new(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);

    public static EmailLinks Links(string? baseUrl = LinkBaseUrl) =>
        new(Options.Create(new EmailOptions { LinkBaseUrl = baseUrl }));

    /// <summary>Whether email with links can be sent, as configured.</summary>
    public static EmailAvailability Availability(bool canSend, string? baseUrl = LinkBaseUrl)
    {
        var sender = new Mock<IEmailSender>();
        sender.SetupGet(s => s.CanSend).Returns(canSend);
        return new EmailAvailability(sender.Object, Links(baseUrl));
    }

    public static AccountEmail Account(string? email = Address, string? pending = null) =>
        new(UserId, Username, email, pending);

    public static TwoFactorState State(bool enabled = true, bool isAdmin = false, int codesLeft = 10) =>
        new(Username, enabled, codesLeft, isAdmin);
}
