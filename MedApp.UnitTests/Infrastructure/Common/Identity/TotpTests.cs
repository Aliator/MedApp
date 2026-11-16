using System.Text;
using FluentAssertions;
using MedApp.Infrastructure.Common.Identity;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

[TestFixture]
public sealed class TotpTests
{
    // RFC 6238, appendix B: the SHA-1 secret, and its 8-digit codes cut to the 6 an authenticator app shows.
    private static readonly byte[] RfcKey = Encoding.ASCII.GetBytes("12345678901234567890");

    [TestCase(59L, "287082")]
    [TestCase(1111111109L, "081804")]
    [TestCase(1111111111L, "050471")]
    [TestCase(1234567890L, "005924")]
    [TestCase(2000000000L, "279037")]
    [TestCase(20000000000L, "353130")]
    public void Compute_MatchesTheRfcTestVectors(long unixSeconds, string expected) =>
        Totp.Compute(RfcKey, Totp.TimeStep(DateTimeOffset.FromUnixTimeSeconds(unixSeconds))).Should().Be(expected);

    [Test]
    public void Match_CodeForNow_ReturnsItsStep()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1111111111);

        Totp.Match(RfcKey, "050471", now).Should().Be(Totp.TimeStep(now));
    }

    [Test]
    public void Match_CodeFromOneStepEitherSide_IsAccepted()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1111111111);
        var step = Totp.TimeStep(now);

        Totp.Match(RfcKey, Totp.Compute(RfcKey, step - 1), now).Should().Be(step - 1);
        Totp.Match(RfcKey, Totp.Compute(RfcKey, step + 1), now).Should().Be(step + 1);
    }

    [Test]
    public void Match_CodeFromTwoStepsAway_IsRefused()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1111111111);
        var step = Totp.TimeStep(now);

        Totp.Match(RfcKey, Totp.Compute(RfcKey, step - 2), now).Should().BeNull();
        Totp.Match(RfcKey, Totp.Compute(RfcKey, step + 2), now).Should().BeNull();
    }

    [TestCase("050 471")]
    public void Match_SpacesAreIgnored(string code) =>
        Totp.Match(RfcKey, code, DateTimeOffset.FromUnixTimeSeconds(1111111111)).Should().NotBeNull();

    [TestCase("")]
    [TestCase("05047")]
    [TestCase("0504711")]
    [TestCase("05047a")]
    [TestCase("ABCDE-23456")]
    public void Match_AnythingButSixDigits_NeverMatches(string code) =>
        Totp.Match(RfcKey, code, DateTimeOffset.FromUnixTimeSeconds(1111111111)).Should().BeNull();

    [Test]
    public void Base32_RoundTripsAndDecodesAsAppsDo()
    {
        var bytes = Encoding.ASCII.GetBytes("Hello!\xDE\xAD\xBE\xEF");

        Base32.Decode(Base32.Encode(bytes)).Should().Equal(bytes);
        Base32.Encode(Encoding.ASCII.GetBytes("foobar")).Should().Be("MZXW6YTBOI");
        Base32.Decode("mzxw 6ytb oi======").Should().Equal(Encoding.ASCII.GetBytes("foobar"));
    }

    [Test]
    public void Base32_InvalidCharacter_Throws()
    {
        var act = () => Base32.Decode("ABC1");

        act.Should().Throw<FormatException>();
    }
}
