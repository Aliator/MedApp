using FluentAssertions;
using MedApp.Domain.Contacts;

namespace MedApp.UnitTests.Domain.Contacts;

/// <summary>Postal addresses, UK postcodes and UK phone numbers: format only, with synthetic values.</summary>
[TestFixture]
public sealed class ContactFormatsTests
{
    [Test]
    public void PostalAddress_Join_TrimsDropsEmptyLinesAndLineBreaks_AndSplitGivesThemBack()
    {
        var stored = PostalAddress.Join(["  1 Imaginary Lane ", "", null, "Fiction\r\nton", "   "]);

        stored.Should().Be("1 Imaginary Lane\nFiction ton");
        PostalAddress.Split(stored).Should().Equal("1 Imaginary Lane", "Fiction ton");
    }

    [Test]
    public void PostalAddress_NoLines_IsNull_AndSplitsToNone()
    {
        PostalAddress.Join(null).Should().BeNull();
        PostalAddress.Join([" ", ""]).Should().BeNull();
        PostalAddress.Split(null).Should().BeEmpty();
        PostalAddress.Split("").Should().BeEmpty();
    }

    [Test]
    public void PostalAddress_MaxStoredLength_HoldsEveryLineAtItsLongest() =>
        PostalAddress.Join(Enumerable.Repeat(new string('a', PostalAddress.MaxLineLength), PostalAddress.MaxLines))!
            .Length.Should().Be(PostalAddress.MaxStoredLength);

    [TestCase("ZZ1 1ZZ", "ZZ1 1ZZ")]
    [TestCase(" zz11zz ", "ZZ1 1ZZ")]
    [TestCase("z z 9 9 3 v z", "ZZ99 3VZ")]
    [TestCase("ZZ1Z1ZZ", "ZZ1Z 1ZZ")]
    [TestCase("gir0aa", "GIR 0AA")]
    public void UkPostcodes_Valid_NormaliseToUpperCaseWithOneSpace(string typed, string stored)
    {
        UkPostcodes.IsValidOrEmpty(typed).Should().BeTrue();
        UkPostcodes.Normalise(typed).Should().Be(stored);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void UkPostcodes_None_IsValidAndNull(string? typed)
    {
        UkPostcodes.IsValidOrEmpty(typed).Should().BeTrue();
        UkPostcodes.Normalise(typed).Should().BeNull();
    }

    [TestCase("ZZ")]
    [TestCase("1ZZ 1ZZ")]
    [TestCase("ZZ1 ZZ1")]
    [TestCase("ZZZ1 1ZZ")]
    [TestCase("ZZ1 1Z")]
    [TestCase("ZZ1-1ZZ")]
    public void UkPostcodes_NotAPostcode_IsInvalid(string typed) =>
        UkPostcodes.IsValidOrEmpty(typed).Should().BeFalse();

    [TestCase("07700 900123")]
    [TestCase("07700900123")]
    [TestCase("+44 7700 900123")]
    [TestCase("+44 (0) 7700 900123")]
    [TestCase("0044 7700-900-123")]
    [TestCase("(07700) 900.123")]
    public void UkPhoneNumbers_Mobile_NormalisesToInternationalForm(string typed)
    {
        UkPhoneNumbers.IsMobileOrEmpty(typed).Should().BeTrue();
        UkPhoneNumbers.NormaliseMobile(typed).Should().Be("+447700900123");
    }

    [TestCase("01632 960000")]
    [TestCase("0770090012")]
    [TestCase("077009001234")]
    [TestCase("+1 7700 900123")]
    [TestCase("07700 9OO123")]
    [TestCase("447700900123")]
    public void UkPhoneNumbers_NotAMobile_IsInvalidAndNormalisesToNull(string typed)
    {
        UkPhoneNumbers.IsMobileOrEmpty(typed).Should().BeFalse();
        UkPhoneNumbers.NormaliseMobile(typed).Should().BeNull();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void UkPhoneNumbers_None_IsValid(string? typed)
    {
        UkPhoneNumbers.IsMobileOrEmpty(typed).Should().BeTrue();
        UkPhoneNumbers.IsUkNumberOrEmpty(typed).Should().BeTrue();
        UkPhoneNumbers.NormaliseMobile(typed).Should().BeNull();
        UkPhoneNumbers.Tidy(typed).Should().BeNull();
    }

    [TestCase("01632 960000")]
    [TestCase("0113 496 0000")]
    [TestCase("+44 1632 960000")]
    [TestCase("07700 900123")]
    [TestCase("020 7946 0000")]
    public void UkPhoneNumbers_UkNumber_IsValid(string typed) =>
        UkPhoneNumbers.IsUkNumberOrEmpty(typed).Should().BeTrue();

    [TestCase("0163296")]
    [TestCase("00 1632 960000")]
    [TestCase("1632 960000")]
    [TestCase("+33 1 23 45 67 89")]
    [TestCase("01632 960000 ext 2")]
    public void UkPhoneNumbers_NotAUkNumber_IsInvalid(string typed) =>
        UkPhoneNumbers.IsUkNumberOrEmpty(typed).Should().BeFalse();

    [Test]
    public void UkPhoneNumbers_Tidy_TrimsAndMakesRunsOfSpacesOne() =>
        UkPhoneNumbers.Tidy("  01632   960000 ").Should().Be("01632 960000");
}
