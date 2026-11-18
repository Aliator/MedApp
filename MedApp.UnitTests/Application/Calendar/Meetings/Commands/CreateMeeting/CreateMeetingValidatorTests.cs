using FluentAssertions;
using MedApp.UnitTests.Common.Fixtures;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Meetings.Commands.CreateMeeting;
using MedApp.UnitTests.Application.Calendar.StaffCalendars;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Meetings.Commands.CreateMeeting;

[TestFixture]
public sealed class CreateMeetingValidatorTests
{
    private readonly CreateMeetingValidator _validator = new(InMemoryClosedDays.WorkingDays(CreateClosedDay()));

    private static CreateMeetingCommand Valid() =>
        new(Title, Notes, At(NextMonday, 10), At(NextMonday, 11), [StaffUserId, OtherStaffUserId], AdminActor);

    private static List<Guid> DistinctIds(int count) =>
        Enumerable.Range(1, count).Select(i => new Guid(i, 0, 0, new byte[8])).ToList();

    [Test]
    public void Validate_ValidMeeting_Passes()
    {
        _validator.ValidateNow(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_WholeWorkingDayWithoutNotes_Passes()
    {
        _validator.ValidateNow(Valid() with { StartUtc = At(NextMonday, 8), EndUtc = At(NextMonday, 18), Notes = null })
            .IsValid.Should().BeTrue();
    }

    [TestCaseSource(typeof(BlockTimeCases), nameof(BlockTimeCases.ValidInSummer))]
    public void Validate(DateTime start, DateTime end)
    {
        _validator.ValidateNow(Valid() with { StartUtc = start, EndUtc = end }).IsValid.Should().BeTrue();
    }

    [TestCaseSource(typeof(BlockTimeCases), nameof(BlockTimeCases.OutsideTheDefaultDay))]
    public void ValidateWithoutThePerson(DateTime start, DateTime end, string message)
    {
        // The validator can't know whose working day it is: the handler checks it under the calendar lock.
        _validator.ValidateNow(Valid() with { StartUtc = start, EndUtc = end }).IsValid.Should().BeTrue(message);
    }

    [TestCaseSource(typeof(BlockTimeCases), nameof(BlockTimeCases.Invalid))]
    public void Validate(DateTime start, DateTime end, string message)
    {
        _validator.ValidateNow(Valid() with { StartUtc = start, EndUtc = end })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(message);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_NoTitle_Fails(string title)
    {
        _validator.ValidateNow(Valid() with { Title = title })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Title is required.");
    }

    [Test]
    public void Validate_TitleAtTheLimit_Passes()
    {
        _validator.ValidateNow(Valid() with { Title = new string('a', MeetingLimits.MaxTitleLength) }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_TitleTooLong_Fails()
    {
        _validator.ValidateNow(Valid() with { Title = new string('a', MeetingLimits.MaxTitleLength + 1) })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Title must not exceed 100 characters.");
    }

    [Test]
    public void Validate_NotesAtTheLimit_Passes()
    {
        _validator.ValidateNow(Valid() with { Notes = new string('a', MeetingLimits.MaxNotesLength) }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NotesTooLong_Fails()
    {
        _validator.ValidateNow(Valid() with { Notes = new string('a', MeetingLimits.MaxNotesLength + 1) })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Notes must not exceed 500 characters.");
    }

    [Test]
    public void Validate_NoAttendees_Fails()
    {
        _validator.ValidateNow(Valid() with { AttendeeUserIds = [] })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(MeetingValidation<CreateMeetingCommand>.NoAttendees);
    }

    [Test]
    public void Validate_AttendeesLeftOut_Fails()
    {
        _validator.ValidateNow(Valid() with { AttendeeUserIds = null! })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(MeetingValidation<CreateMeetingCommand>.NoAttendees);
    }

    [Test]
    public void Validate_TheMostAttendeesWithRepeats_Passes()
    {
        var ids = DistinctIds(MeetingLimits.MaxAttendees);

        _validator.ValidateNow(Valid() with { AttendeeUserIds = [.. ids, ids[0], ids[1]] }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_TooManyAttendees_Fails()
    {
        _validator.ValidateNow(Valid() with { AttendeeUserIds = DistinctIds(MeetingLimits.MaxAttendees + 1) })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("A meeting can have at most 100 attendees.");
    }

    [Test]
    public void Validate_EmptyAttendeeId_Fails()
    {
        _validator.ValidateNow(Valid() with { AttendeeUserIds = [StaffUserId, Guid.Empty] })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Attendee ids must not be empty.");
    }

    [Test]
    public void Validate_OnAClosedDay_SaysTheClinicIsClosed()
    {
        _validator.ValidateNow(Valid() with { StartUtc = At(ClosedWednesday, 10), EndUtc = At(ClosedWednesday, 11) })
            .Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(WorkingHours.ClosedDay(ClosedWednesday, ClosedDayName));
    }
}
