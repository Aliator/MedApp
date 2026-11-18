using FluentAssertions;
using MedApp.Application.Common.Email;
using MedApp.Domain.Authentication;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Common.Email;

/// <summary>The one email that carries a secret: a short-lived two-factor code, with nothing else in it.</summary>
[TestFixture]
public sealed class TwoFactorCodeEmailTests
{
    [TestCase(true, "to sign in to MedApp")]
    [TestCase(false, "to confirm a change to how you sign in to MedApp")]
    public void TwoFactorCode_HoldsTheCodeWhatItsForHowLongAndWhatToDo(bool forSignIn, string purpose)
    {
        var email = EmailTemplates.TwoFactorCode(Address, EmailedCode, forSignIn, EmailCode.Lifetime);

        email.To.Should().Be(Address);
        email.Category.Should().Be(EmailCategory.Code);
        email.Subject.Should().Be(EmailTemplates.TwoFactorCodeSubject).And.NotContain(EmailedCode);
        email.TextBody.Should().Contain(EmailedCode).And.Contain(purpose).And.Contain("10 minutes")
            .And.Contain("If you didn't ask for a code").And.Contain("change your password");
        email.HtmlBody.Should().Contain(EmailedCode).And.Contain("lang=\"en-GB\"").And.Contain("<title>");
    }

    [TestCase(10.0, "for 10 minutes")]
    [TestCase(3.5, "for 3 minutes")]
    [TestCase(1.0, "for 1 minute.")]
    [TestCase(0.4, "for 1 minute.")]
    public void TwoFactorCode_SaysHowLongItWorks_NeverMore(double minutes, string says) =>
        EmailTemplates.TwoFactorCode(Address, EmailedCode, forSignIn: true, TimeSpan.FromMinutes(minutes)).TextBody.Should().Contain(says);

    [Test]
    public void TwoFactorCode_HoldsNothingElse()
    {
        var email = EmailTemplates.TwoFactorCode(Address, EmailedCode, forSignIn: true, EmailCode.Lifetime);

        // No username, no link, no greeting: only the code, its purpose, its life and what to do.
        email.TextBody.Should().NotContain(Username).And.NotContain("http").And.NotContain("Hello");
        email.HtmlBody.Should().NotContain("<a ").And.NotContain("<img");
        email.ToString().Should().NotContain(EmailedCode).And.NotContain(Address);
    }

    [Test]
    public void TwoFactorCode_EncodesTheCode() =>
        EmailTemplates.TwoFactorCode(Address, "<b>", forSignIn: true, EmailCode.Lifetime).HtmlBody.Should().Contain("&lt;b&gt;");

    [Test]
    public void Notice_EmailCodesEnabled_SaysCodesComeToThisAddress() =>
        EmailTemplates.Notice(Address, Username, SecurityNotice.EmailCodesEnabled, null).TextBody
            .Should().Contain("codes by email").And.Contain("this address");

    [TestCase("jane.doe@example.test", "j•••@example.test")]
    [TestCase("j@example.test", "j•••@example.test")]
    [TestCase("a@b@example.test", "a•••@example.test")]
    [TestCase("no-at-sign", "•••")]
    [TestCase("@example.test", "•••")]
    [TestCase("\U0001F600x@example.test", "\U0001F600•••@example.test")]
    public void Mask_HidesAllButTheFirstCharacterOfTheName(string address, string expected) =>
        EmailAddressMask.Mask(address).Should().Be(expected);
}
