using AutoFixture;
using FluentValidation;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Meetings.Commands.UpdateMeeting;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.Domain.Notifications;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Meetings.Commands.UpdateMeeting;

[TestFixture]
public sealed class UpdateMeetingHandlerTests : MeetingHandlerTestBase
{
    private UpdateMeetingHandler _handler = null!;

    /// <summary>The meeting as stored, tracked for the change: 10:00 to 11:00 next Monday, for both members of staff.</summary>
    private Meeting _tracked = null!;

    [SetUp]
    public void SetUp()
    {
        GivenMeeting(NewMeeting(At(NextMonday, 10), At(NextMonday, 11)));
        _handler = Fixture.Create<UpdateMeetingHandler>();
    }

    /// <summary>The repository reads this meeting, and loads a separate copy of it for the change.</summary>
    private void GivenMeeting(Meeting meeting, Meeting? tracked = null)
    {
        _tracked = tracked ?? Copy(meeting);
        Meetings.Setup(r => r.GetAsync(meeting.Id, It.IsAny<CancellationToken>())).ReturnsAsync(meeting);
        Meetings.Setup(r => r.GetForUpdateAsync(meeting.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_tracked);
    }

    /// <summary>An account that has gone: not among <see cref="CalendarTestConstants.Staff"/>.</summary>
    private static readonly Guid GoneUserId = Guid.Parse("0000000d-0000-0000-0000-0000000000ee");

    /// <summary>
    /// The admin changes the meeting: by default, nothing but the title, to <see cref="OtherTitle"/>, from a copy read
    /// when it was last updated (a day ago).
    /// </summary>
    private static UpdateMeetingCommand Command(
        DateTime? startUtc = null,
        DateTime? endUtc = null,
        IReadOnlyList<Guid>? attendees = null,
        CalendarActor? actor = null,
        string title = OtherTitle,
        DateTime? expectedLastUpdated = null) =>
        new(
            MeetingId,
            $" {title} ",
            $" {Notes} ",
            startUtc ?? At(NextMonday, 10),
            endUtc ?? At(NextMonday, 11),
            attendees ?? [StaffUserId, OtherStaffUserId],
            expectedLastUpdated ?? Now.AddDays(-1),
            actor ?? AdminActor);

    [Test]
    public async Task Handle_Changes_AreSavedOnTheTrackedMeeting()
    {
        await _handler.Handle(Command(At(NextMonday, 14), At(NextMonday, 15, 30), [OtherStaffUserId, AdminUserId, AdminUserId]), Token);

        _tracked.Title.Should().Be(OtherTitle);
        _tracked.Notes.Should().Be(Notes);
        _tracked.StartUtc.Should().Be(At(NextMonday, 14));
        _tracked.EndUtc.Should().Be(At(NextMonday, 15, 30));
        _tracked.LastUpdated.Should().Be(Now);
        _tracked.CreatedAtUtc.Should().Be(Now.AddDays(-1));
        _tracked.Attendees.Select(a => a.StaffUserId).Should().Equal(OtherStaffUserId, AdminUserId);
        _tracked.Attendees.Should().OnlyContain(a => a.MeetingId == MeetingId);
        Meetings.Verify(r => r.SaveAsync(Token), Times.Once);
        Meetings.Verify(r => r.GetAsync(MeetingId, Token), Times.Once);
        Meetings.Verify(r => r.GetForUpdateAsync(MeetingId, Token), Times.Once);
    }

    [Test]
    public async Task Handle_NoNotes_StoresThemEmpty()
    {
        await _handler.Handle(Command() with { Notes = null }, Token);

        _tracked.Notes.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AttendeesChanged_HoldsTheLocksOfEveryoneGoingBeforeAndAfter()
    {
        await _handler.Handle(Command(attendees: [OtherStaffUserId, AdminUserId]), Token);

        VerifyLockedOnce(StaffUserId, OtherStaffUserId, AdminUserId);
    }

    [Test]
    public async Task Handle_ClashCheck_ReadsTheNewAttendeesCalendarsOnTheNewDay()
    {
        var tuesday = NextMonday.AddDays(1);

        await _handler.Handle(Command(At(tuesday, 10), At(tuesday, 11), [OtherStaffUserId, AdminUserId]), Token);

        var read = CalendarReads.Should().ContainSingle().Subject;
        read.StaffUserIds.Should().Equal(OtherStaffUserId, AdminUserId);
        read.From.Should().Be(tuesday);
        read.To.Should().Be(tuesday);
        read.Token.Should().Be(Token);
    }

    [Test]
    public async Task Handle_OverlappingItsOwnOldTime_IsNotAClash()
    {
        var self = NewMeeting(At(NextMonday, 10), At(NextMonday, 11));
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, meetings: [self]));
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, meetings: [self]));

        await _handler.Handle(Command(At(NextMonday, 10, 30), At(NextMonday, 11, 30)), Token);

        Meetings.Verify(r => r.SaveAsync(Token), Times.Once);
    }

    [Test]
    public async Task Handle_AnAttendeeHasSomethingOn_ThrowsConflictNamingThemWithoutSaving()
    {
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, templateBlocks: [CreateTemplateBlock(12, 13)]));

        var act = () => _handler.Handle(Command(At(NextMonday, 12), At(NextMonday, 13)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{OtherStaffUsername} has Lunch, 12:00 to 13:00.");
        Meetings.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NotFound_ThrowsNotFoundWithoutLocking()
    {
        Meetings.Setup(r => r.GetAsync(MeetingId, It.IsAny<CancellationToken>())).ReturnsAsync((Meeting?)null);

        var act = () => _handler.Handle(Command(), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Meeting '{MeetingId}' was not found.");
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_CancelledMeanwhile_ThrowsNotFoundWithoutSaving()
    {
        Meetings.Setup(r => r.GetForUpdateAsync(MeetingId, It.IsAny<CancellationToken>())).ReturnsAsync((Meeting?)null);

        var act = () => _handler.Handle(Command(), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Meeting '{MeetingId}' was not found.");
        Meetings.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_SomeoneAddedAnAttendeeMeanwhile_ThrowsConflictWithoutSaving()
    {
        var read = NewMeeting(At(NextMonday, 10), At(NextMonday, 11), [StaffUserId]);
        GivenMeeting(read, NewMeeting(At(NextMonday, 10), At(NextMonday, 11), [StaffUserId, AdminUserId]));

        var act = () => _handler.Handle(Command(attendees: [StaffUserId, OtherStaffUserId]), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(MeetingLimits.ChangedMeanwhile);
        VerifyLockedOnce(StaffUserId, OtherStaffUserId);
        Meetings.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_SomeoneRemovedAnAttendeeMeanwhile_StillSaves()
    {
        GivenMeeting(
            NewMeeting(At(NextMonday, 10), At(NextMonday, 11)),
            NewMeeting(At(NextMonday, 10), At(NextMonday, 11), [StaffUserId]));

        await _handler.Handle(Command(), Token);

        _tracked.Attendees.Select(a => a.StaffUserId).Should().Equal(StaffUserId, OtherStaffUserId);
        Meetings.Verify(r => r.SaveAsync(Token), Times.Once);
    }

    [Test]
    public async Task Handle_ChangedSinceTheCallerReadIt_ThrowsConflictWithoutLockingOrSaving()
    {
        // Another admin saved a change an hour ago; this caller's screen is from before it.
        var changed = NewMeeting(At(NextMonday, 10), At(NextMonday, 11));
        changed.LastUpdated = Now.AddHours(-1);
        GivenMeeting(changed);

        var act = () => _handler.Handle(Command(At(NextMonday, 14), At(NextMonday, 15)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(MeetingLimits.ChangedMeanwhile);
        VerifyNotLocked();
        Meetings.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ChangedBetweenTheFirstReadAndTheLocks_ThrowsConflictWithoutSaving()
    {
        var saved = NewMeeting(At(NextMonday, 10), At(NextMonday, 11));
        var changedMeanwhile = Copy(saved);
        changedMeanwhile.LastUpdated = Now.AddSeconds(-1);
        changedMeanwhile.Title = "Changed by another admin";
        GivenMeeting(saved, changedMeanwhile);

        var act = () => _handler.Handle(Command(), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(MeetingLimits.ChangedMeanwhile);
        _tracked.Title.Should().Be("Changed by another admin");
        Meetings.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ExpectedTimeWithAnOffset_IsComparedAsTheSameMoment()
    {
        // 09:00 UTC a day ago, as a local time: the same moment, so it isn't a change.
        var local = Now.AddDays(-1).ToLocalTime();

        await _handler.Handle(Command(expectedLastUpdated: local), Token);

        Meetings.Verify(r => r.SaveAsync(Token), Times.Once);
    }

    [Test]
    public async Task Handle_AnAttendeeWhoseAccountHasGone_IsDroppedWithoutAnErrorOrANotification()
    {
        // A training day that had started when a leaver's account went: they stayed on it (only meetings to come lose
        // them), and the admin moves its end, keeping everyone the dialog showed.
        GivenMeeting(NewMeeting(At(NextMonday, 10), At(NextMonday, 11), [StaffUserId, GoneUserId]));

        await _handler.Handle(Command(At(NextMonday, 10), At(NextMonday, 12), [StaffUserId, GoneUserId]), Token);

        _tracked.Attendees.Select(a => a.StaffUserId).Should().Equal(StaffUserId);
        Meetings.Verify(r => r.SaveAsync(Token), Times.Once);
        Sent.Should().ContainSingle().Which.Notification.UserId.Should().Be(StaffUserId);
    }

    [Test]
    public async Task Handle_AnAttendeeWhoseAccountHasGoneTakenOff_TellsNobody()
    {
        GivenMeeting(NewMeeting(At(NextMonday, 10), At(NextMonday, 11), [StaffUserId, GoneUserId]));

        await _handler.Handle(Command(attendees: [StaffUserId]), Token);

        _tracked.Attendees.Select(a => a.StaffUserId).Should().Equal(StaffUserId);
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_OnlyAttendeesWhoseAccountsHaveGoneLeft_ThrowsValidationWithoutLocking()
    {
        GivenMeeting(NewMeeting(At(NextMonday, 10), At(NextMonday, 11), [GoneUserId]));

        var act = () => _handler.Handle(Command(attendees: [GoneUserId]), Token);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { PropertyName = "AttendeeUserIds", ErrorMessage = MeetingStaff.NobodyLeft });
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_AddingSomeoneWhoseAccountHasGone_ThrowsNotFound()
    {
        // Only those already going may have gone: anyone newly added must be staff.
        var act = () => _handler.Handle(Command(attendees: [StaffUserId, GoneUserId]), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Staff member(s) not found: {GoneUserId}.");
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_MeetingOnAPastDay_ThrowsConflictWithoutLocking()
    {
        var friday = Today.AddDays(-3);
        GivenMeeting(NewMeeting(At(friday, 10), At(friday, 11)));

        var act = () => _handler.Handle(Command(), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(CalendarGuards.PastDay);
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_MovedToAPastDay_ThrowsConflictWithoutLocking()
    {
        var friday = Today.AddDays(-3);

        var act = () => _handler.Handle(Command(At(friday, 10), At(friday, 11)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(CalendarGuards.PastDay);
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_UnknownAttendee_ThrowsNotFoundWithoutLocking()
    {
        var stranger = Guid.Parse("0000000d-0000-0000-0000-0000000000ff");

        var act = () => _handler.Handle(Command(attendees: [stranger]), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Staff member(s) not found: {stranger}.");
        VerifyNotLocked();
    }

    [Test]
    public async Task Handle_OnlyTheTitleChanged_TellsNobody()
    {
        await _handler.Handle(Command(), Token);

        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Moved_TellsEachAttendeeWhereTo()
    {
        await _handler.Handle(Command(At(NextMonday, 14), At(NextMonday, 15)), Token);

        Sent.Select(s => s.Notification.UserId).Should().BeEquivalentTo([StaffUserId, OtherStaffUserId]);
        Sent.Should().OnlyContain(s => s.Token == Token);
        Sent.Should().OnlyContain(s => s.Notification.Kind == NotificationKind.MeetingChanged);
        Sent.Should().OnlyContain(s => s.Notification.Title == "Meeting moved");
        Sent.Should().OnlyContain(s =>
            s.Notification.Message == $"{AdminUsername} moved '{OtherTitle}' to Monday 23 November, 14:00 to 15:00. Please reply again.");
        Sent.Should().OnlyContain(s => s.Notification.CalendarDate == NextMonday);
    }

    [Test]
    public async Task Handle_AttendeesChanged_InvitesThoseAddedAndTellsThoseTakenOff()
    {
        var thursday = NextMonday.AddDays(3);

        await _handler.Handle(Command(At(thursday, 9), At(thursday, 10), [OtherStaffUserId, AdminUserId], StaffActor with { Permissions = TestPermissions.Admin }), Token);

        // The actor (calendar.staff, taken off) isn't told about their own change; the admin is invited, the other moved.
        Sent.Should().HaveCount(2);
        var invited = Sent.Single(s => s.Notification.UserId == AdminUserId).Notification;
        invited.Kind.Should().Be(NotificationKind.MeetingInvited);
        invited.Message.Should().Be($"{StaffUsername} invited you to '{OtherTitle}', Thursday 26 November, 09:00 to 10:00.");
        invited.CalendarDate.Should().Be(thursday);
        Sent.Single(s => s.Notification.UserId == OtherStaffUserId).Notification.Kind.Should().Be(NotificationKind.MeetingChanged);
    }

    [Test]
    public async Task Handle_AttendeeTakenOff_IsToldItsCancelledForThemWithTheOldTime()
    {
        var thursday = NextMonday.AddDays(3);

        await _handler.Handle(Command(At(thursday, 9), At(thursday, 10), [OtherStaffUserId]), Token);

        var cancelled = Sent.Single(s => s.Notification.UserId == StaffUserId).Notification;
        cancelled.Kind.Should().Be(NotificationKind.MeetingCancelled);
        cancelled.Title.Should().Be("Meeting cancelled");
        cancelled.Message.Should().Be($"{AdminUsername} took you off '{OtherTitle}', Monday 23 November, 10:00 to 11:00.");
        cancelled.CalendarDate.Should().Be(NextMonday);
    }

    [Test]
    public async Task Handle_Changed_ReturnsItWithItsPeopleByUsername()
    {
        var response = await _handler.Handle(Command(attendees: [OtherStaffUserId, AdminUserId]), Token);

        response.Id.Should().Be(MeetingId);
        response.Title.Should().Be(OtherTitle);
        response.Date.Should().Be(NextMonday);
        response.OrganiserUsername.Should().Be(AdminUsername);
        response.Attendees.Select(a => a.Username).Should().Equal(AdminUsername, OtherStaffUsername);
        response.CanEdit.Should().BeTrue();
        response.LastUpdated.Should().Be(Now);
        Identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
    }

    // Replies

    /// <summary>Gives this attendee of the stored meeting a reply, as of an hour ago.</summary>
    private void GivenReply(Guid staffUserId, MeetingReply reply)
    {
        var attendee = _tracked.Attendees.Single(a => a.StaffUserId == staffUserId);
        attendee.Reply = reply;
        attendee.RepliedAtUtc = Now.AddHours(-1);
    }

    [TestCase(10, 30, 11, 30)]
    [TestCase(10, 0, 11, 30)]
    [TestCase(9, 30, 11, 0)]
    public async Task Handle_Moved_PutsEveryReplyBackToNoReply(int startHour, int startMinute, int endHour, int endMinute)
    {
        GivenReply(StaffUserId, MeetingReply.Declined);
        GivenReply(OtherStaffUserId, MeetingReply.Accepted);

        var response = await _handler.Handle(
            Command(At(NextMonday, startHour, startMinute), At(NextMonday, endHour, endMinute), [StaffUserId, OtherStaffUserId, AdminUserId]), Token);

        _tracked.Attendees.Should().HaveCount(3).And.OnlyContain(a => a.Reply == MeetingReply.NoReply && a.RepliedAtUtc == null);
        response.Attendees.Should().OnlyContain(a => a.Reply == "NoReply");
        Meetings.Verify(r => r.SaveAsync(Token), Times.Once);
    }

    [Test]
    public async Task Handle_NotMoved_KeepsReplies_AndThoseAddedStartWithNone()
    {
        GivenReply(StaffUserId, MeetingReply.Declined);
        GivenReply(OtherStaffUserId, MeetingReply.Accepted);

        await _handler.Handle(Command(attendees: [StaffUserId, OtherStaffUserId, AdminUserId]), Token);

        var replies = _tracked.Attendees.ToDictionary(a => a.StaffUserId);
        replies[StaffUserId].Reply.Should().Be(MeetingReply.Declined);
        replies[StaffUserId].RepliedAtUtc.Should().Be(Now.AddHours(-1));
        replies[OtherStaffUserId].Reply.Should().Be(MeetingReply.Accepted);
        replies[AdminUserId].Reply.Should().Be(MeetingReply.NoReply);
        replies[AdminUserId].RepliedAtUtc.Should().BeNull();
        Sent.Should().NotContain(s => s.Notification.Kind == NotificationKind.MeetingChanged, "nobody is asked to reply again");
    }

    [Test]
    public async Task Handle_NotMoved_SomeoneWhoDeclinedHasBookedTheTimeSince_IsStillSaved()
    {
        // They declined, then booked an appointment at 10:00: a change of title keeps their decline, so isn't refused.
        GivenReply(StaffUserId, MeetingReply.Declined);
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, appointments: [CreateAppointment(startUtc: At(NextMonday, 10))], meetings: [_tracked]));

        await _handler.Handle(Command(), Token);

        Meetings.Verify(r => r.SaveAsync(Token), Times.Once);
        CalendarReads.Single().StaffUserIds.Should().Equal(OtherStaffUserId);
    }

    [Test]
    public async Task Handle_Moved_SomeoneWhoDeclinedIsBusyAtTheNewTime_IsSaved_KeepingTheirDecline()
    {
        // They declined and aren't going, so what they have on at 14:00 doesn't stop the move; nor are they asked again.
        GivenReply(StaffUserId, MeetingReply.Declined);
        GivenReply(OtherStaffUserId, MeetingReply.Accepted);
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, appointments: [CreateAppointment(startUtc: At(NextMonday, 14))]));

        await _handler.Handle(Command(At(NextMonday, 14), At(NextMonday, 15)), Token);

        Meetings.Verify(r => r.SaveAsync(Token), Times.Once);
        var replies = _tracked.Attendees.ToDictionary(a => a.StaffUserId);
        replies[StaffUserId].Reply.Should().Be(MeetingReply.Declined);
        replies[StaffUserId].RepliedAtUtc.Should().Be(Now.AddHours(-1));
        replies[OtherStaffUserId].Reply.Should().Be(MeetingReply.NoReply);
        // The others checked first, then those who declined, at the new time, each read once.
        CalendarReads.Select(r => r.StaffUserIds).Should().BeEquivalentTo([new[] { OtherStaffUserId }, new[] { StaffUserId }]);
        CalendarReads.Should().OnlyContain(r => r.From == NextMonday && r.Token == Token);
        Sent.Single(s => s.Notification.UserId == StaffUserId).Notification.Message
            .Should().EndWith(MeetingNotifications.DeclineStands);
        Sent.Single(s => s.Notification.UserId == OtherStaffUserId).Notification.Message
            .Should().EndWith(MeetingNotifications.ReplyAgain);
    }

    [Test]
    public async Task Handle_Moved_SomeoneWhoDeclinedIsFreeAtTheNewTime_IsAskedAgain()
    {
        GivenReply(StaffUserId, MeetingReply.Declined);

        await _handler.Handle(Command(At(NextMonday, 14), At(NextMonday, 15)), Token);

        _tracked.Attendees.Should().OnlyContain(a => a.Reply == MeetingReply.NoReply && a.RepliedAtUtc == null);
        Sent.Single(s => s.Notification.UserId == StaffUserId).Notification.Message.Should().EndWith(MeetingNotifications.ReplyAgain);
    }

    [Test]
    public async Task Handle_Moved_SomeoneGoingIsBusyAtTheNewTime_ThrowsConflictNamingThem()
    {
        GivenReply(StaffUserId, MeetingReply.Declined);
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, appointments: [CreateAppointment(startUtc: At(NextMonday, 14))]));

        var act = () => _handler.Handle(Command(At(NextMonday, 14), At(NextMonday, 15)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{OtherStaffUsername} has another appointment, 14:00 to 14:30.");
        Meetings.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
