using FluentAssertions;
using FluentValidation.TestHelper;
using MedApp.Application.Appointments.Queries.SearchAppointments;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Appointments.Queries.SearchAppointments;

[TestFixture]
public sealed class SearchAppointmentsValidatorTests
{
    private readonly SearchAppointmentsValidator _validator = new();

    private static SearchAppointmentsQuery Valid() => AppointmentsTestConstants.CreateSearchQuery();

    private IEnumerable<string> Errors(SearchAppointmentsQuery query) =>
        _validator.Validate(query).Errors.Select(e => e.ErrorMessage);

    [Test]
    public void Validate_Defaults_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EveryFilter_Passes()
    {
        var query = Valid() with
        {
            Sort = "DUEDATE",
            PatientId = Guid.NewGuid(),
            PathwayId = Guid.NewGuid(),
            StageId = Guid.NewGuid(),
            StaffUserId = Guid.NewGuid(),
            Statuses = ["Booked", "requested"],
            From = new DateOnly(2026, 11, 16),
            To = new DateOnly(2026, 11, 16),
            Search = "synthetic"
        };

        _validator.Validate(query).IsValid.Should().BeTrue();
    }

    [TestCase("didNotAttend")]
    [TestCase("DIDNOTATTEND")]
    public void Validate_DidNotAttendView_Passes(string view) =>
        _validator.TestValidate(AppointmentsTestConstants.CreateSearchQuery() with { View = view }).ShouldNotHaveAnyValidationErrors();

    [Test]
    public void Validate_UnknownView_Fails() =>
        _validator.TestValidate(AppointmentsTestConstants.CreateSearchQuery() with { View = "everything" })
            .ShouldHaveValidationErrorFor(x => x.View);

    [Test]
    public void Validate_PageZero_Fails()
    {
        Errors(Valid() with { Page = 0 }).Should().Equal("Page must be 1 or more.");
    }

    [TestCase(0)]
    [TestCase(101)]
    public void Validate_PageSizeOutOfRange_Fails(int pageSize)
    {
        Errors(Valid() with { PageSize = pageSize }).Should().Equal("Page size must be between 1 and 100.");
    }

    [Test]
    public void Validate_UnknownSort_Fails()
    {
        Errors(Valid() with { Sort = "notes" }).Should().Equal("Sort must be one of: start, requested, dueDate, patient.");
    }

    [Test]
    public void Validate_UnknownStatus_Fails()
    {
        Errors(Valid() with { Statuses = ["Booked", "Pending"] })
            .Should().Equal("Each status must be one of: Requested, Booked, Attended, DidNotAttend, Cancelled, EnteredInError.");
    }

    [Test]
    public void Validate_StatusByNumber_Fails()
    {
        _validator.Validate(Valid() with { Statuses = ["1"] }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_ToBeforeFrom_Fails()
    {
        Errors(Valid() with { From = new DateOnly(2026, 11, 17), To = new DateOnly(2026, 11, 16) })
            .Should().Equal("The end of the date range must not be before its start.");
    }

    [Test]
    public void Validate_OnlyOneEndOfTheRange_Passes()
    {
        _validator.Validate(Valid() with { From = new DateOnly(2026, 11, 17) }).IsValid.Should().BeTrue();
        _validator.Validate(Valid() with { To = new DateOnly(2026, 11, 17) }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_SearchTooLong_Fails()
    {
        Errors(Valid() with { Search = new string('a', 201) }).Should().Equal("Search must not exceed 200 characters.");
    }
}
