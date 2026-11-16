using FluentAssertions;
using MedApp.Domain.Patients;
using static MedApp.UnitTests.Common.Constants.NhsTestConstants;

namespace MedApp.UnitTests.Domain.Patients;

[TestFixture]
public sealed class NhsNumbersTests
{
    [TestCase(NhsNumber)]
    [TestCase(OtherNhsNumber)]
    [TestCase(NhsNumberWithSpaces)]
    [TestCase(" 9990000018 ")]
    [TestCase("9990000034")]
    [TestCase("9990000042")]
    public void IsValid_ValidNumber_IsTrue(string number) => NhsNumbers.IsValid(number).Should().BeTrue();

    [TestCase(WrongCheckDigit)]
    [TestCase(CheckDigitTen)]
    [TestCase(NineDigits)]
    [TestCase("99900000188")]
    [TestCase("999-000-0018")]
    [TestCase("99900000I8")]
    [TestCase("９９９００００018")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void IsValid_InvalidNumber_IsFalse(string? number) => NhsNumbers.IsValid(number).Should().BeFalse();

    [Test]
    public void IsValid_CheckDigitElevenBecomesZero_IsTrue()
    {
        // Eleven minus a remainder of 0 is 11, which becomes a check digit of 0: the first 999 stem whose sum divides by 11.
        var stem = Enumerable.Range(999_000_000, 200).Select(n => n.ToString())
            .First(s => NhsNumbers.CheckDigit(s) == 0);
        var sum = stem.Select((c, i) => (c - '0') * (10 - i)).Sum();

        (sum % 11).Should().Be(0);
        NhsNumbers.IsValid(stem + "0").Should().BeTrue();
    }

    [TestCase("0000000000")]
    [TestCase("1111111111")]
    [TestCase("2222222222")]
    [TestCase("3333333333")]
    [TestCase("4444444444")]
    [TestCase("5555555555")]
    [TestCase("6666666666")]
    [TestCase("7777777777")]
    [TestCase("8888888888")]
    [TestCase("9999999999")]
    [TestCase("0123456789")]
    [TestCase("9876543210")]
    [TestCase("012 345 6789")]
    public void IsPlaceholder_PlaceholdersThatPassTheCheck_AreCaught(string number)
    {
        NhsNumbers.IsValid(number).Should().BeTrue("each passes the Modulus 11 check, which is why it needs its own");
        NhsNumbers.IsPlaceholder(number).Should().BeTrue();
    }

    [TestCase(NhsNumber)]
    [TestCase("1234567890")]
    [TestCase("111111111")]
    [TestCase("111111111a")]
    [TestCase(null)]
    public void IsPlaceholder_AnythingElse_IsFalse(string? number) => NhsNumbers.IsPlaceholder(number).Should().BeFalse();

    [Test]
    public void CheckDigit_FirstNineDigits_IsTheModulus11Digit()
    {
        NhsNumbers.CheckDigit("999000001").Should().Be(8);
        NhsNumbers.CheckDigit("999000014").Should().BeNull("its check digit would be 10, which is never issued");
    }

    [Test]
    public void Normalise_TakesOutSpaces_AndBlankIsNull()
    {
        NhsNumbers.Normalise(" 999 000\t0018 ").Should().Be(NhsNumber);
        NhsNumbers.Normalise("  ").Should().BeNull();
        NhsNumbers.Normalise(null).Should().BeNull();
    }

    [Test]
    public void IsTenDigits_IgnoresTheCheckDigit()
    {
        NhsNumbers.IsTenDigits(WrongCheckDigit).Should().BeTrue();
        NhsNumbers.IsTenDigits(NhsNumberWithSpaces).Should().BeTrue();
        NhsNumbers.IsTenDigits(NineDigits).Should().BeFalse();
        NhsNumbers.IsTenDigits("99900000a8").Should().BeFalse();
        NhsNumbers.IsTenDigits(null).Should().BeFalse();
    }

    [Test]
    public void Format_TenDigits_GroupsThemThreeThreeFour()
    {
        NhsNumbers.Format(NhsNumber).Should().Be(NhsNumberWithSpaces);
        NhsNumbers.Format(" 99 90 00 00 18").Should().Be(NhsNumberWithSpaces);
    }

    [Test]
    public void Format_NotTenDigits_ReturnsItAsItWas()
    {
        NhsNumbers.Format(NineDigits).Should().Be(NineDigits);
        NhsNumbers.Format("abc").Should().Be("abc");
    }
}
