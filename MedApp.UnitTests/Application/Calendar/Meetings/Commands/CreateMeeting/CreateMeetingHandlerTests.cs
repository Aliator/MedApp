using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Meetings.Commands.CreateMeeting;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Notifications;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Meetings.Commands.CreateMeeting;

[TestFixture]
public sealed class CreateMeetingHandlerTests : MeetingHandlerTestBase
{
    private CreateMeetingHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<CreateMeetingHandler>();

    /// <summary>The admin arranges an MDT, 10:00 to 11:00 next Monday, for the two members of staff.</summary>
    private static CreateMeetingCommand Command(
        DateTime? startUtc = null,
        DateTime? endUtc = null,
        IReadOnlyList<Guid>? attendees = null,
        CalendarActor? actor = null) =>
        new(
            $"  {Title}  ",
            $" {Notes} ",
            startUtc ?? At(NextMonday, 10),
            endUtc ?? At(NextMonday, 11),
            attendees ?? [StaffUserId, OtherStaffUserId],
            actor ?? AdminActor);

    [Test]
    public async Task Handle_ValidMeeting_AddsItTrimmedWithEachAttendeeOnce()
    {
        await _handler.Handle(Command(attendees: [StaffUserId, OtherStaffUserId, StaffUserId]), Token);

        var meeting = Added.Should().ContainSingle().Subject;
        meeting.Id.Should().NotBeEmpty();
        meeting.Title.Should().Be(Title);
        meeting.Notes.Should().Be(Notes);
        meeting.StartUtc.Should().Be(At(NextMonday, 10));
        meeting.EndUtc.Should().Be(At(NextMonday, 11));
        meeting.OrganiserUserId.Should().Be(AdminUserId);
        meeting.CreatedAtUtc.Should().Be(Now);
        meeting.LastUpdated.Should().Be(Now);
        meeting.Attendees.Select(a => a.StaffUserId).Should().Equal(StaffUserId, OtherStaffUserId);
        meeting.Attendees.Should().OnlyContain(a => a.MeetingId == meeting.Id);
        Meetings.Verify(r => r.AddAsync(meeting, Token), Times.Once);
    }

    [Test]
    public async Task Handle_NoNotes_StoresThemEmpty()
    {
        await _handler.Handle(Command() with { Notes = null }, Token);

        Added.Single().Notes.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_UnspecifiedTimes_AreTakenAsUtc()
    {
        var start = DateTime.SpecifyKind(At(NextMonday, 10), DateTimeKind.Unspecified);
        var end = DateTime.SpecifyKind(At(NextMonday, 11), DateTimeKind.Unspecified);

        await _handler.Handle(Command(start, end), Token);

        Added.Single().StartUtc.Kind.Should().Be(DateTimeKind.Utc);
        Added.Single().EndUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public async Task Handle_ValidMeeting_HoldsEveryAttendeesLockAndChecksTheirCalendarsThatDay()
    {
        await _handler.Handle(Command(attendees: [StaffUserId, OtherStaffUserId, StaffUserId]), Token);

        VerifyLockedOnce(StaffUserId, OtherStaffUserId);
        var read = CalendarReads.Should().ContainSingle().Subject;
        read.StaffUserIds.Should().Equal(StaffUserId, OtherStaffUserId);
        read.From.Should().Be(NextMonday);
        read.To.Should().Be(NextMonday);
        read.Token.Should().Be(Token);
        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);
    }

    [Test]
    public async Task Handle_AnAttendeeHasSomethingOn_ThrowsConflictNamingThemAndWhatWithoutAdding()
    {
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, templateBlocks: [CreateTemplateBlock(12, 13)]));

        var act = () => _handler.Handle(Command(At(NextMonday, 12, 30), At(NextMonday, 13, 30)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{OtherStaffUsername} has Lunch, 12:00 to 13:00.");
        Added.Should().BeEmpty();
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AnAttendeeIsInAnotherMeeting_ThrowsConflictNamingThatMeeting()
    {
        var huddle = NewMeeting(At(NextMonday, 10, 30), At(NextMonday, 11, 30), [StaffUserId], OtherMeetingId, OtherTitle);
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, meetings: [huddle]));

        var act = () => _handler.Handle(Command(), Token);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"{StaffUsername} has the meeting '{OtherTitle}', 10:30 to 11:30.");
    }

    [Test]
    public async Task Handle_SeveralAttendeesBusy_NamesTheFirstInTheStaffListWhateverTheOrderAsked()
    {
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, blocks: [CreateBlock(At(NextMonday, 10), At(NextMonday, 11))]));
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, templateBlocks: [CreateTemplateBlock(10, 11)]));

        var act = () => _handler.Handle(Command(attendees: [OtherStaffUserId, StaffUserId]), Token);

        // The identity service lists calendar.staff first (Staff), so they're checked first.
        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{StaffUsername} has Training, 10:00 to 11:00.");
    }

    [Test]
    public async Task Handle_TouchingSomethingOn_IsFree()
    {
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, templateBlocks: [CreateTemplateBlock(11, 12)]));

        await _handler.Handle(Command(), Token);

        Added.Should().ContainSingle();
    }

    [Test]
    public async Task Handle_PastDay_ThrowsConflictWithoutLockingOrReading()
    {
        var friday = Today.AddDays(-3);

        var act = () => _handler.Handle(Command(At(friday, 10), At(friday, 11)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(CalendarGuards.PastDay);
        VerifyNotLocked();
        CalendarReads.Should().BeEmpty();
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_EarlierToday_IsAllowed()
    {
        await _handler.Handle(Command(At(Today, 8), At(Today, 8, 30)), Token);

        Added.Should().ContainSingle();
    }

    [Test]
    public async Task Handle_UnknownAttendee_ThrowsNotFoundNamingThemWithoutLocking()
    {
        var stranger = Guid.Parse("0000000d-0000-0000-0000-0000000000ff");

        var act = () => _handler.Handle(Command(attendees: [StaffUserId, stranger]), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Staff member(s) not found: {stranger}.");
        VerifyNotLocked();
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Created_InvitesEachAttendeeButTheAdmin()
    {
        await _handler.Handle(Command(attendees: [StaffUserId, AdminUserId]), Token);

        var (notification, token) = Sent.Should().ContainSingle().Subject;
        token.Should().Be(Token);
        notification.UserId.Should().Be(StaffUserId);
        notification.Kind.Should().Be(NotificationKind.MeetingInvited);
        notification.Title.Should().Be("Meeting invitation");
        notification.Message.Should().Be($"{AdminUsername} invited you to '{Title}', Monday 23 November, 10:00 to 11:00.");
        notification.CalendarDate.Should().Be(NextMonday);
        notification.CreatedAtUtc.Should().Be(Now);
    }

    [Test]
    public async Task Handle_Created_ReturnsItWithItsPeopleByUsername()
    {
        var response = await _handler.Handle(Command(), Token);

        var meeting = Added.Single();
        response.Id.Should().Be(meeting.Id);
        response.Title.Should().Be(Title);
        response.Notes.Should().Be(Notes);
        response.Date.Should().Be(NextMonday);
        response.StartUtc.Should().Be(At(NextMonday, 10));
        response.EndUtc.Should().Be(At(NextMonday, 11));
        response.OrganiserUserId.Should().Be(AdminUserId);
        response.OrganiserUsername.Should().Be(AdminUsername);
        response.Attendees.Select(a => a.Username).Should().Equal(OtherStaffUsername, StaffUsername);
        response.CanEdit.Should().BeTrue();
        response.CreatedAtUtc.Should().Be(Now);
        Identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
    }

    [Test]
    public async Task Handle_ByAnActorWhoIsntAnAdmin_ReturnsItAsNotEditable()
    {
        // Only admins reach the handler through the API; the response still follows the actor given.
        var response = await _handler.Handle(Command(actor: StaffActor), Token);

        response.CanEdit.Should().BeFalse();
        Added.Single().OrganiserUserId.Should().Be(StaffUserId);
    }
}
