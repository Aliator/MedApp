using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetAvailability;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Queries.GetAvailability;

[TestFixture]
public sealed class GetAvailabilityValidatorTests
{
    private readonly GetAvailabilityValidator _validator = new();

    private static GetAvailabilityQuery Valid() => new([StaffUserId, OtherStaffUserId], NextMonday, NextFriday, 60, null);

    private static List<Guid> DistinctIds(int count) =>
        Enumerable.Range(1, count).Select(i => new Guid(i, 0, 0, new byte[8])).ToList();

    private void ExpectError(GetAvailabilityQuery query, string message) =>
        _validator.Validate(query).Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(message);

    [Test]
    public void Validate_ValidQuery_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_MovingAMeeting_Passes()
    {
        _validator.Validate(Valid() with { ExceptMeetingId = Guid.NewGuid() }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NobodyChosen_Fails()
    {
        ExpectError(Valid() with { StaffUserIds = [] }, "Choose at least one member of staff.");
    }

    [Test]
    public void Validate_PeopleLeftOut_Fails()
    {
        ExpectError(Valid() with { StaffUserIds = null! }, "Choose at least one member of staff.");
    }

    [Test]
    public void Validate_TheMostPeopleWithRepeats_Passes()
    {
        var ids = DistinctIds(MeetingLimits.MaxPeople);

        _validator.Validate(Valid() with { StaffUserIds = [.. ids, ids[0]] }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_TooManyPeople_Fails()
    {
        ExpectError(Valid() with { StaffUserIds = DistinctIds(MeetingLimits.MaxPeople + 1) }, "Suggested times look at most 100 people.");
    }

    [Test]
    public void Validate_NoFrom_Fails()
    {
        // An end the day after it, so the range itself is fine.
        ExpectError(Valid() with { From = default, To = default(DateOnly).AddDays(1) }, "From is required.");
    }

    [Test]
    public void Validate_NoTo_FailsWithThatAlone()
    {
        ExpectError(Valid() with { To = default }, "To is required.");
    }

    [Test]
    public void Validate_EndBeforeStart_Fails()
    {
        ExpectError(Valid() with { To = NextMonday.AddDays(-1) }, "The end of the range must not be before its start.");
    }

    [Test]
    public void Validate_OneDay_Passes()
    {
        _validator.Validate(Valid() with { To = NextMonday }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_TheMostDays_Passes()
    {
        _validator.Validate(Valid() with { To = NextMonday.AddDays(WorkingHours.MaxDays - 1) }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_TooManyDays_Fails()
    {
        ExpectError(Valid() with { To = NextMonday.AddDays(WorkingHours.MaxDays) }, "Suggested times look at most 31 days.");
    }

    [TestCase(5)]
    [TestCase(45)]
    [TestCase(GetAvailabilityValidator.MaxDurationMinutes)]
    public void Validate_Length_Passes(int minutes)
    {
        _validator.Validate(Valid() with { DurationMinutes = minutes }).IsValid.Should().BeTrue();
    }

    [TestCase(0)]
    [TestCase(-5)]
    [TestCase(7)]
    [TestCase(GetAvailabilityValidator.MaxDurationMinutes + 5)]
    public void Validate_Length_Fails(int minutes)
    {
        ExpectError(Valid() with { DurationMinutes = minutes }, "The length must be a multiple of 5 minutes, at most 10 hours.");
    }
}
