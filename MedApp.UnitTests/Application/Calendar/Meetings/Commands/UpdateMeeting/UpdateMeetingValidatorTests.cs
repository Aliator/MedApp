using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Meetings.Commands.UpdateMeeting;
using MedApp.UnitTests.Application.Calendar.StaffCalendars;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Meetings.Commands.UpdateMeeting;

[TestFixture]
public sealed class UpdateMeetingValidatorTests
{
    private readonly UpdateMeetingValidator _validator = new();

    private static UpdateMeetingCommand Valid() =>
        new(MeetingId, Title, Notes, At(NextMonday, 10), At(NextMonday, 11), [StaffUserId], Now.AddDays(-1), AdminActor);

    [Test]
    public void Validate_ValidChange_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoMeetingId_Fails()
    {
        _validator.Validate(Valid() with { MeetingId = Guid.Empty })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Meeting id is required.");
    }

    [TestCaseSource(typeof(BlockTimeCases), nameof(BlockTimeCases.Invalid))]
    public void Validate(DateTime start, DateTime end, string message)
    {
        _validator.Validate(Valid() with { StartUtc = start, EndUtc = end })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(message);
    }

    [Test]
    public void Validate_NoTitle_Fails()
    {
        _validator.Validate(Valid() with { Title = " " })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Title is required.");
    }

    [Test]
    public void Validate_NotesTooLong_Fails()
    {
        _validator.Validate(Valid() with { Notes = new string('a', MeetingLimits.MaxNotesLength + 1) })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Notes must not exceed 500 characters.");
    }

    [Test]
    public void Validate_NoAttendees_Fails()
    {
        _validator.Validate(Valid() with { AttendeeUserIds = [] })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(MeetingValidation<UpdateMeetingCommand>.NoAttendees);
    }

    [Test]
    public void Validate_NoExpectedLastUpdated_Fails()
    {
        _validator.Validate(Valid() with { ExpectedLastUpdated = null })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(UpdateMeetingValidator.NoExpectedLastUpdated);
    }
}
