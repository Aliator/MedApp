using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.UnitTests.Application.Pathways.Common;

[TestFixture]
public sealed class PathwayEnumsTests
{
    [TestCase("high", PatientPathwayPriority.High)]
    [TestCase(" Urgent ", PatientPathwayPriority.Urgent)]
    public void Parse_KnownName_ReturnsTheValueIgnoringCaseAndSpaces(string value, PatientPathwayPriority expected)
    {
        PathwayEnums.Parse<PatientPathwayPriority>(value).Should().Be(expected);
    }

    [TestCase("Whenever")]
    [TestCase("1")]
    [TestCase(" ")]
    public void Parse_UnknownNameOrNumber_ThrowsArgumentException(string value)
    {
        var act = () => PathwayEnums.Parse<PatientPathwayPriority>(value);

        act.Should().Throw<ArgumentException>()
            .WithMessage($"'{value}' is not a {nameof(PatientPathwayPriority)}.*")
            .Which.ParamName.Should().Be("value");
    }

    [TestCase("InReview", true)]
    [TestCase("inreview", true)]
    [TestCase("Finished", false)]
    [TestCase("3", false)]
    [TestCase(null, false)]
    public void IsStatus_Value_AcceptsOnlyStatusNames(string? value, bool expected)
    {
        PathwayEnums.IsStatus(value).Should().Be(expected);
    }

    [TestCase("Required", true)]
    [TestCase("suggested", true)]
    [TestCase("None", true)]
    [TestCase("Sometimes", false)]
    [TestCase("2", false)]
    [TestCase(null, false)]
    public void IsVisitRequirement_Value_AcceptsOnlyVisitRequirementNames(string? value, bool expected)
    {
        PathwayEnums.IsVisitRequirement(value).Should().Be(expected);
    }

    [Test]
    public void VisitRequirementNames_ListsEveryNameInOrder()
    {
        PathwayEnums.VisitRequirementNames.Should().Be("None, Suggested, Required");
    }
}
