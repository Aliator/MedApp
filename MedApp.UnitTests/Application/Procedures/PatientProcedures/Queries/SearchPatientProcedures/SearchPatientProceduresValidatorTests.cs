using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;

[TestFixture]
public sealed class SearchPatientProceduresValidatorTests
{
    private readonly SearchPatientProceduresValidator _validator = new();

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
            OverdueOnly = true, DueFrom = Today, DueTo = Today
        };

        _validator.Validate(query).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_UnknownSort_Fails()
    {
        _validator.Validate(CreateSearchQuery() with { Sort = "colour" }).IsValid.Should().BeFalse();
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
}
