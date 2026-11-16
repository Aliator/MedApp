using FluentAssertions;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;

namespace MedApp.UnitTests.Application.Procedures.Timeline.Queries.GetPatientTimeline;

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
            Before = new TimelineCursor(Now, ProcedureId).Encode(),
            Kinds = ["blocks", "DueDates"],
            ProcedureIds = [ProcedureId],
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
    public void Validate_TooManyProcedures_Fails()
    {
        var ids = Enumerable.Range(0, GetPatientTimelineValidator.MaxProcedureIds + 1).Select(_ => Guid.NewGuid()).ToList();

        _validator.Validate(CreateTimelineQuery() with { ProcedureIds = ids }).IsValid.Should().BeFalse();
    }
}
