using FluentAssertions;
using MedApp.Domain.Terminology;
using static MedApp.UnitTests.Common.Constants.NhsTestConstants;

namespace MedApp.UnitTests.Domain.Terminology;

[TestFixture]
public sealed class SnomedCtTests
{
    [TestCase(SnomedCode)]
    [TestCase("71388002")] // Procedure
    [TestCase("387713003")] // Surgical procedure
    [TestCase(ExtensionConceptCode)]
    public void IsConceptId_ValidConcept_IsTrue(string code)
    {
        SnomedCt.IsConceptId(code).Should().BeTrue();
        SnomedCt.WhyNotConceptId(code).Should().BeNull();
    }

    [TestCase(null, "digits only")]
    [TestCase("", "digits only")]
    [TestCase("8014600a", "digits only")]
    [TestCase(" 80146002", "digits only")]
    [TestCase("80146", "6 to 18 digits")]
    [TestCase("1234567890123456789", "6 to 18 digits")]
    [TestCase("080146002", "doesn't start with 0")]
    [TestCase(WrongVerhoeff, "check digit")]
    [TestCase(DescriptionId, "not a concept")]
    [TestCase(RelationshipId, "not a concept")]
    public void WhyNotConceptId_NotAConceptId_SaysWhy(string? code, string why)
    {
        SnomedCt.WhyNotConceptId(code).Should().Contain(why);
        SnomedCt.IsConceptId(code).Should().BeFalse();
    }

    [Test]
    public void HasValidCheckDigit_EveryOtherLastDigit_Fails()
    {
        var stem = SnomedCode[..^1];
        var valid = Enumerable.Range(0, 10).Where(d => SnomedCt.HasValidCheckDigit(stem + d)).ToList();

        valid.Should().Equal(SnomedCode[^1] - '0');
    }

    [Test]
    public void HasValidCheckDigit_SwappedNeighbours_Fails()
    {
        // Verhoeff catches every transposition of adjacent digits.
        SnomedCt.HasValidCheckDigit("81046002").Should().BeFalse();
    }

    [Test]
    public void Partition_IsTheTwoDigitsBeforeTheCheckDigit()
    {
        SnomedCt.Partition(SnomedCode).Should().Be("00");
        SnomedCt.Partition(DescriptionId).Should().Be("01");
        SnomedCt.Partition(ExtensionConceptCode).Should().Be("10");
    }
}
