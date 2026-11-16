using MedApp.Application.Authentication.TwoFactor.Common;
using FluentAssertions;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Common.Email;

[TestFixture]
public sealed class EmailTests
{
    // EmailLinks

    [Test]
    public void Links_PutTheTokenInTheFragmentEscaped()
    {
        var links = Links(LinkBaseUrl + "/");

        links.CanLink.Should().BeTrue();
        links.ResetPassword(UserId, Token).Should().Be($"{LinkBaseUrl}/reset-password#user={UserId:D}&token=synthetic%2Btoken%2Fwith%3Dchars");
        links.ConfirmEmail(UserId, Token).Should().StartWith($"{LinkBaseUrl}/confirm-email#user=");
        links.SignIn().Should().Be($"{LinkBaseUrl}/login");
    }

    [TestCase(null)]
    [TestCase("  ")]
    public void Links_NoBaseUrl_MakeNone(string? baseUrl)
    {
        var links = Links(baseUrl);

        links.CanLink.Should().BeFalse();
        links.ResetPassword(UserId, Token).Should().BeNull();
        links.ConfirmEmail(UserId, Token).Should().BeNull();
        links.SignIn().Should().BeNull();
    }

    [TestCase(true, LinkBaseUrl, true)]
    [TestCase(false, LinkBaseUrl, false)]
    [TestCase(true, null, false)]
    public void Availability_NeedsAServerAndABaseUrl(bool canSend, string? baseUrl, bool expected) =>
        Availability(canSend, baseUrl).CanSendLinks.Should().Be(expected);

    // EmailTemplates

    [Test]
    public void ResetPassword_HasTheLinkTheAccountAndHowLongItLasts()
    {
        var email = EmailTemplates.ResetLink(Address, Username, "https://x.test/reset-password#token=a&b");

        email.To.Should().Be(Address);
        email.Subject.Should().Be(EmailTemplates.ResetLinkSubject);
        email.TextBody.Should().Contain(Username).And.Contain("https://x.test/reset-password#token=a&b").And.Contain("30 minutes");
        // HTML-encoded in the HTML.
        email.HtmlBody.Should().Contain("https://x.test/reset-password#token=a&amp;b").And.Contain("lang=\"en-GB\"").And.Contain("<title>");
    }

    [Test]
    public void ConfirmEmail_HasTheLink()
    {
        var email = EmailTemplates.ConfirmAddress(Address, Username, "https://x.test/confirm");

        email.Subject.Should().Be(EmailTemplates.ConfirmAddressSubject);
        email.TextBody.Should().Contain("https://x.test/confirm").And.Contain("24 hours");
    }

    [Test]
    public void Templates_EncodeEveryValue()
    {
        var email = EmailTemplates.ConfirmAddress(Address, "<script>alert(1)</script>", "https://x.test/\"><b>");

        email.HtmlBody.Should().NotContain("<script>").And.NotContain("\"><b>");
        email.HtmlBody.Should().Contain("&lt;script&gt;");
    }

    [TestCaseSource(nameof(AllNotices))]
    public void Notice_EveryKindHasItsOwnSubjectAndTellsWhatToDoIfItWasntYou(SecurityNotice notice)
    {
        var email = EmailTemplates.Notice(Address, Username, notice, $"{LinkBaseUrl}/login");

        email.Subject.Should().NotBeNullOrWhiteSpace();
        email.TextBody.Should().Contain(Username).And.Contain("administrator").And.Contain($"{LinkBaseUrl}/login");
    }

    [Test]
    public void Notice_SubjectsDiffer() =>
        AllNotices().Select(n => EmailTemplates.Notice(Address, Username, n, null).Subject).Should().OnlyHaveUniqueItems();

    [Test]
    public void Notice_RecoveryCodeUsed_SaysHowManyAreLeft()
    {
        EmailTemplates.Notice(Address, Username, SecurityNotice.RecoveryCodeUsed, null, 7).TextBody.Should().Contain("you have 7 left");
        EmailTemplates.Notice(Address, Username, SecurityNotice.RecoveryCodeUsed, null).TextBody.Should().NotContain("left");
    }

    [Test]
    public void Notice_NoLink_HasNone() =>
        EmailTemplates.Notice(Address, Username, SecurityNotice.TwoFactorEnabled, null).HtmlBody.Should().NotContain("<a ");

    [Test]
    public void Notice_UnknownKind_Throws()
    {
        var act = () => EmailTemplates.Notice(Address, Username, (SecurityNotice)999, null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Message_ToString_NeverShowsTheAddressOrBody()
    {
        var email = EmailTemplates.ResetLink(Address, Username, "https://x.test/secret-token");

        // Only the category: never the address, the subject (which says what happened) or the body.
        email.ToString().Should().Be("EmailMessage { Category = Link }");
    }

    private static IEnumerable<SecurityNotice> AllNotices() => Enum.GetValues<SecurityNotice>();

    // SecurityNotices

    [Test]
    public async Task Notices_SendToTheConfirmedAddress()
    {
        var (notices, sent, _) = NoticesWith(Account());

        await notices.SendAsync(UserId, SecurityNotice.TwoFactorDisabled, CancellationToken.None);

        sent.Should().ContainSingle(m => m.To == Address && m.TextBody.Contains($"{LinkBaseUrl}/login"));
    }

    [Test]
    public async Task Notices_SendToTheAddressGivenInstead()
    {
        var (notices, sent, _) = NoticesWith(Account());

        await notices.SendAsync(UserId, SecurityNotice.AddressChanged, CancellationToken.None, toAddress: OtherAddress);

        sent.Should().ContainSingle(m => m.To == OtherAddress);
    }

    [Test]
    public async Task Notices_NoConfirmedAddress_SendNothing()
    {
        var (notices, sent, _) = NoticesWith(Account(email: null, pending: OtherAddress));

        await notices.SendAsync(UserId, SecurityNotice.TwoFactorEnabled, CancellationToken.None);

        sent.Should().BeEmpty();
    }

    [Test]
    public async Task Notices_ToOtherAdmins_SkipsTheOneItsAboutAndNamesThem()
    {
        var other = Guid.NewGuid();
        var (notices, sent, _) = NoticesWith(null, [new AccountContact(UserId, Username, Address), new AccountContact(other, "other.admin", OtherAddress)]);

        await notices.SendToOtherAdminsAsync(SecurityNotice.AdminTwoFactorRemoved, UserId, Username, CancellationToken.None);

        var message = sent.Should().ContainSingle().Subject;
        message.To.Should().Be(OtherAddress);
        message.Category.Should().Be(EmailCategory.Notice);
        message.TextBody.Should().Contain(Username).And.Contain("other.admin");
        message.ToString().Should().NotContain(OtherAddress);
    }

    [Test]
    public async Task Notices_NoAccount_SendNothing()
    {
        var (notices, sent, _) = NoticesWith(null);

        await notices.SendAsync(UserId, SecurityNotice.TwoFactorEnabled, CancellationToken.None);

        sent.Should().BeEmpty();
    }

    private static (SecurityNotices Notices, List<EmailMessage> Sent, Mock<IAccountEmailService> Emails) NoticesWith(
        AccountEmail? account, IReadOnlyList<AccountContact>? admins = null)
    {
        var twoFactor = new Mock<ITwoFactorService>();
        twoFactor.Setup(t => t.GetAdminContactsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(admins ?? []);
        var emails = new Mock<IAccountEmailService>();
        emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        var sent = new List<EmailMessage>();
        var sender = new Mock<IEmailSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => sent.Add(m)).Returns(Task.CompletedTask);
        return (new SecurityNotices(emails.Object, twoFactor.Object, sender.Object, Links(), NullLogger<SecurityNotices>.Instance), sent, emails);
    }
}
