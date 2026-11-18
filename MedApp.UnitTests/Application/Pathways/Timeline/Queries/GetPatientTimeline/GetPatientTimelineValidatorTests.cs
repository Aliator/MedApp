using FluentAssertions;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;

namespace MedApp.UnitTests.Application.Pathways.Timeline.Queries.GetPatientTimeline;

[TestFixture]
public sealed class GetPatientTimelineValidatorTests
{
    private readonly GetPatientTimelineValidator _validator = new();

    [Test]
    public void Validate_DefaultQuery_Passes()
    {
        _validator.Validate(CreateTimelineQuery()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EveryFilterAndACursor_Passes()
    {
        var query = CreateTimelineQuery() with
        {
            Before = new TimelineCursor(Now, PathwayId).Encode(),
            Kinds = ["blocks", "DueDates"],
            PathwayIds = [PathwayId],
        };

        _validator.Validate(query).IsValid.Should().BeTrue();
    }

    [TestCase(0)]
    [TestCase(101)]
    public void Validate_PageSizeOutOfRange_Fails(int pageSize)
    {
        _validator.Validate(CreateTimelineQuery(pageSize: pageSize)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_CursorNotFromAPage_Fails()
    {
        var result = _validator.Validate(CreateTimelineQuery() with { Before = "made-up" });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Before");
    }

    [Test]
    public void Validate_UnknownKind_Fails()
    {
        _validator.Validate(CreateTimelineQuery() with { Kinds = ["Notes", "Exploded"] }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_TooManyPathways_Fails()
    {
        var ids = Enumerable.Range(0, GetPatientTimelineValidator.MaxPathwayIds + 1).Select(_ => Guid.NewGuid()).ToList();

        _validator.Validate(CreateTimelineQuery() with { PathwayIds = ids }).IsValid.Should().BeFalse();
    }
}
