using FluentAssertions;
using MedApp.Domain.Sites;

namespace MedApp.UnitTests.Domain.Sites;

[TestFixture]
public sealed class UkPostcodesTests
{
    [TestCase("ZZ99 3AA", "ZZ99 3AA")]
    [TestCase("zz993aa", "ZZ99 3AA")]
    [TestCase(" zz99  3aa ", "ZZ99 3AA")]
    [TestCase("A1 1AA", "A1 1AA")]
    [TestCase("A11AA", "A1 1AA")]
    [TestCase("AB1C2DE", "AB1C 2DE")]
    [TestCase("A1B 2CD", "A1B 2CD")]
    public void IsValidAndNormalise_UkFormats(string text, string expected)
    {
        UkPostcodes.IsValid(text).Should().BeTrue();
        UkPostcodes.Normalise(text).Should().Be(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("ZZ99")]
    [TestCase("ZZ99 3A")]
    [TestCase("99ZZ 3AA")]
    [TestCase("ZZZ9 3AA")]
    [TestCase("ZZ99 33A")]
    [TestCase("ZZ99 3AA X")]
    public void IsValid_NotUkFormats_IsFalse(string? text) => UkPostcodes.IsValid(text).Should().BeFalse();
}
