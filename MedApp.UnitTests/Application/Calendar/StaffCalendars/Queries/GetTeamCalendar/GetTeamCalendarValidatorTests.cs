using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetTeamCalendar;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Queries.GetTeamCalendar;

[TestFixture]
public sealed class GetTeamCalendarValidatorTests
{
    private readonly GetTeamCalendarValidator _validator = new();

    private static GetTeamCalendarQuery Valid() => new(NextMonday, [StaffUserId, OtherStaffUserId], AdminActor);

    private static List<Guid> DistinctIds(int count) =>
        Enumerable.Range(1, count).Select(i => new Guid(i, 0, 0, new byte[8])).ToList();

    [Test]
    public void Validate_SomePeople_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NobodyNamed_PassesForEveryone()
    {
        _validator.Validate(Valid() with { StaffUserIds = [] }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoDate_Fails()
    {
        _validator.Validate(Valid() with { Date = default })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Date is required.");
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
        _validator.Validate(Valid() with { StaffUserIds = DistinctIds(MeetingLimits.MaxPeople + 1) })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("The team view shows at most 100 people.");
    }
}
