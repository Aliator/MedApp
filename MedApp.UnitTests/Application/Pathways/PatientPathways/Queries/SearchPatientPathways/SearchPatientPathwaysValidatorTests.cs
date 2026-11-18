using FluentAssertions;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;

[TestFixture]
public sealed class SearchPatientPathwaysValidatorTests
{
    private readonly SearchPatientPathwaysValidator _validator = new();

    [Test]
    public void Validate_DefaultQuery_Passes()
    {
        _validator.Validate(CreateSearchQuery()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EveryFilterSet_Passes()
    {
        var query = CreateSearchQuery() with
        {
            Sort = "Priority", Search = "knee", Statuses = ["InProgress", "blocked"], Priority = "Urgent",
            OverdueOnly = true, DueFrom = Today, DueTo = Today, Priorities = ["Low", "high"], Title = "knee",
            TemplateName = "joint", PatientName = "freya"
        };

        _validator.Validate(query).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_UnknownSort_Fails()
    {
        _validator.Validate(CreateSearchQuery() with { Sort = "colour" }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_UnknownPriorityInPriorities_Fails()
    {
        _validator.Validate(CreateSearchQuery() with { Priorities = ["High", "Whenever"] }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_PatientNameOver200Characters_Fails()
    {
        _validator.Validate(CreateSearchQuery() with { PatientName = new string('a', 201) }).IsValid.Should().BeFalse();
    }

    [TestCase("Finished")]
    [TestCase("2")]
    public void Validate_UnknownStatus_Fails(string status)
    {
        _validator.Validate(CreateSearchQuery() with { Statuses = [status] }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_PageSizeOverMaximum_Fails()
    {
        _validator.Validate(CreateSearchQuery() with { PageSize = 101 }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_DueRangeEndsBeforeItStarts_Fails()
    {
        var query = CreateSearchQuery() with { DueFrom = Today, DueTo = Today.AddDays(-1) };

        _validator.Validate(query).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_TeamsAndStagesUpToTheLimit_Passes()
    {
        var ids = Enumerable.Range(0, PathwaySearch.MaxIdsPerFilter).Select(_ => Guid.NewGuid()).ToList();

        _validator.Validate(CreateSearchQuery() with { TeamIds = ids, StageDefinitionIds = ids }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_TooManyTeamsOrStages_Fails()
    {
        var ids = Enumerable.Range(0, PathwaySearch.MaxIdsPerFilter + 1).Select(_ => Guid.NewGuid()).ToList();

        _validator.Validate(CreateSearchQuery() with { TeamIds = ids }).IsValid.Should().BeFalse();
        _validator.Validate(CreateSearchQuery() with { StageDefinitionIds = ids }).IsValid.Should().BeFalse();
    }
}
