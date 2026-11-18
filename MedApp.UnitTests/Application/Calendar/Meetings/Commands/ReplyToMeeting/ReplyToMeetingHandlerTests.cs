using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Meetings.Commands.ReplyToMeeting;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MedApp.Domain.Notifications;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Meetings.Commands.ReplyToMeeting;

[TestFixture]
public sealed class ReplyToMeetingHandlerTests : MeetingHandlerTestBase
{
    private ReplyToMeetingHandler _handler = null!;

    /// <summary>The meeting as loaded for the reply: 10:00 to 11:00 next Monday, for both members of staff.</summary>
    private Meeting _meeting = null!;

    [SetUp]
    public void SetUp()
    {
        GivenMeeting(NewMeeting(At(NextMonday, 10), At(NextMonday, 11)));
        _handler = Fixture.Create<ReplyToMeetingHandler>();
    }

    private void GivenMeeting(Meeting meeting)
    {
        _meeting = meeting;
        Meetings.Setup(r => r.GetForUpdateAsync(meeting.Id, It.IsAny<CancellationToken>())).ReturnsAsync(meeting);
    }

    private MeetingAttendee Attendee(Guid staffUserId) => _meeting.Attendees.Single(a => a.StaffUserId == staffUserId);

    private Task<MeetingResponse> Reply(string reply, CalendarActor? actor = null) =>
        _handler.Handle(new ReplyToMeetingCommand(MeetingId, reply, actor ?? StaffActor), Token);

    [Test]
    public async Task Handle_Decline_RecordsItHoldingOnlyTheirLock_AndSaves()
    {
        var lastUpdated = _meeting.LastUpdated;

        var response = await Reply("Declined");

        Attendee(StaffUserId).Reply.Should().Be(MeetingReply.Declined);
        Attendee(StaffUserId).RepliedAtUtc.Should().Be(Now);
        Attendee(OtherStaffUserId).Reply.Should().Be(MeetingReply.NoReply);
        _meeting.LastUpdated.Should().Be(lastUpdated, "a reply isn't a change to the meeting");
        VerifyLockedOnce(StaffUserId);
        Meetings.Verify(r => r.GetForUpdateAsync(MeetingId, Token), Times.Once);
        Meetings.Verify(r => r.SaveAsync(Token), Times.Once);
        response.MyReply.Should().Be("Declined");
        response.CanReply.Should().BeTrue();
        response.Attendees.Single(a => a.UserId == StaffUserId).Reply.Should().Be("Declined");
    }

    [Test]
    public async Task Handle_Decline_TellsTheOrganiser()
    {
        await Reply("declined");

        var (notification, token) = Sent.Single();
        token.Should().Be(Token);
        notification.UserId.Should().Be(AdminUserId);
        notification.Kind.Should().Be(NotificationKind.MeetingDeclined);
        notification.Title.Should().Be("Meeting declined");
        notification.Message.Should().Be($"{StaffUsername} declined '{Title}', Monday 23 November, 10:00 to 11:00.");
        notification.CalendarDate.Should().Be(NextMonday);
    }

    [Test]
    public async Task Handle_Accept_IsSaved_AndTellsNobody()
    {
        await Reply("Accepted");

        Attendee(StaffUserId).Reply.Should().Be(MeetingReply.Accepted);
        Meetings.Verify(r => r.SaveAsync(Token), Times.Once);
        Sent.Should().BeEmpty();
        CalendarReads.Should().BeEmpty("only accepting after declining checks the time is still free");
    }

    [Test]
    public async Task Handle_TheSameReplyAgain_SavesNothing_AndDoesntTellTheOrganiserAgain()
    {
        Attendee(StaffUserId).Reply = MeetingReply.Declined;
        Attendee(StaffUserId).RepliedAtUtc = Now.AddHours(-1);

        var response = await Reply("Declined");

        Attendee(StaffUserId).RepliedAtUtc.Should().Be(Now.AddHours(-1));
        Meetings.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
        Sent.Should().BeEmpty();
        response.MyReply.Should().Be("Declined");
    }

    [Test]
    public async Task Handle_AcceptingAfterDeclining_TimeStillFree_IsSaved()
    {
        Attendee(StaffUserId).Reply = MeetingReply.Declined;

        await Reply("Accepted");

        Attendee(StaffUserId).Reply.Should().Be(MeetingReply.Accepted);
        CalendarReads.Should().ContainSingle(r => r.StaffUserIds.SequenceEqual(new[] { StaffUserId })
                                                  && r.From == NextMonday && r.To == NextMonday && r.Token == Token);
        Meetings.Verify(r => r.SaveAsync(Token), Times.Once);
    }

    [Test]
    public async Task Handle_AcceptingAfterDeclining_TimeTakenSince_IsAConflictNamingWhat()
    {
        // The decline freed 10:00 to 11:00, and an appointment was booked at 10:30 since.
        Attendee(StaffUserId).Reply = MeetingReply.Declined;
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, appointments: [CreateAppointment(startUtc: At(NextMonday, 10, 30))], meetings: [_meeting]));

        var act = () => Reply("Accepted");

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"{MeetingReplies.TakenSinceDeclining} That time overlaps another appointment, 10:30 to 11:00.");
        Meetings.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_SomeoneNotGoing_IsForbidden_EvenAnAdmin()
    {
        // The organiser isn't going (not listed), and nobody replies for anyone else.
        var act = () => Reply("Declined", AdminActor);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(MeetingReplies.NotAnAttendee);
        _meeting.Attendees.Should().OnlyContain(a => a.Reply == MeetingReply.NoReply);
        Meetings.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Ended_IsAConflict()
    {
        GivenMeeting(NewMeeting(At(Today, 8), At(Today, 8, 30)));

        var act = () => Reply("Declined");

        await act.Should().ThrowAsync<ConflictException>().WithMessage(MeetingReplies.Ended);
        Meetings.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnderWay_TakesTheReply()
    {
        GivenMeeting(NewMeeting(At(Today, 8, 30), At(Today, 9, 30)));

        await Reply("Declined");

        Attendee(StaffUserId).Reply.Should().Be(MeetingReply.Declined);
    }

    [Test]
    public async Task Handle_CancelledMeeting_IsNotFound()
    {
        // Cancelling deletes a meeting.
        var act = () => _handler.Handle(new ReplyToMeetingCommand(OtherMeetingId, "Accepted", StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        Meetings.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_OrganiserDeclinesTheirOwn_TellsNobody()
    {
        GivenMeeting(NewMeeting(At(NextMonday, 10), At(NextMonday, 11), [AdminUserId, StaffUserId]));

        await Reply("Declined", AdminActor);

        Attendee(AdminUserId).Reply.Should().Be(MeetingReply.Declined);
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_OrganisersAccountGone_TellsNobody()
    {
        var meeting = NewMeeting(At(NextMonday, 10), At(NextMonday, 11));
        GivenMeeting(new Meeting
        {
            Id = meeting.Id,
            Title = meeting.Title,
            StartUtc = meeting.StartUtc,
            EndUtc = meeting.EndUtc,
            OrganiserUserId = Guid.Parse("0000000d-0000-0000-0000-0000000000ee"),
            Attendees = meeting.Attendees
        });

        await Reply("Declined");

        Attendee(StaffUserId).Reply.Should().Be(MeetingReply.Declined);
        Sent.Should().BeEmpty();
    }
}
