using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Meetings.Commands.CancelMeeting;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.Domain.Notifications;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Meetings.Commands.CancelMeeting;

[TestFixture]
public sealed class CancelMeetingHandlerTests : MeetingHandlerTestBase
{
    private CancelMeetingHandler _handler = null!;
    private Meeting _meeting = null!;

    [SetUp]
    public void SetUp()
    {
        GivenMeeting(NewMeeting(At(NextMonday, 10), At(NextMonday, 11), [StaffUserId, OtherStaffUserId, AdminUserId]));
        _handler = Fixture.Create<CancelMeetingHandler>();
    }

    private void GivenMeeting(Meeting meeting)
    {
        _meeting = meeting;
        Meetings.Setup(r => r.GetForUpdateAsync(meeting.Id, It.IsAny<CancellationToken>())).ReturnsAsync(meeting);
    }

    [Test]
    public async Task Handle_UpcomingMeeting_DeletesIt()
    {
        await _handler.Handle(new CancelMeetingCommand(MeetingId, AdminActor), Token);

        Meetings.Verify(r => r.GetForUpdateAsync(MeetingId, Token), Times.Once);
        Meetings.Verify(r => r.DeleteAsync(_meeting, Token), Times.Once);
    }

    [Test]
    public async Task Handle_UpcomingMeeting_TellsEachAttendeeButTheAdmin()
    {
        await _handler.Handle(new CancelMeetingCommand(MeetingId, AdminActor), Token);

        Sent.Select(s => s.Notification.UserId).Should().Equal(StaffUserId, OtherStaffUserId);
        Sent.Should().OnlyContain(s => s.Token == Token);
        var notification = Sent[0].Notification;
        notification.Kind.Should().Be(NotificationKind.MeetingCancelled);
        notification.Title.Should().Be("Meeting cancelled");
        notification.Message.Should().Be($"{AdminUsername} cancelled '{Title}', Monday 23 November, 10:00 to 11:00.");
        notification.CalendarDate.Should().Be(NextMonday);
        notification.CreatedAtUtc.Should().Be(Now);
    }

    [Test]
    public async Task Handle_EarlierToday_IsCancelled()
    {
        GivenMeeting(NewMeeting(At(Today, 8), At(Today, 8, 30)));

        await _handler.Handle(new CancelMeetingCommand(MeetingId, AdminActor), Token);

        Meetings.Verify(r => r.DeleteAsync(_meeting, Token), Times.Once);
    }

    [Test]
    public async Task Handle_NotFound_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new CancelMeetingCommand(OtherMeetingId, AdminActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Meeting '{OtherMeetingId}' was not found.");
        Meetings.Verify(r => r.DeleteAsync(It.IsAny<Meeting>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_PastDay_ThrowsConflictWithoutDeletingOrTellingAnyone()
    {
        var friday = Today.AddDays(-3);
        GivenMeeting(NewMeeting(At(friday, 10), At(friday, 11)));

        var act = () => _handler.Handle(new CancelMeetingCommand(MeetingId, AdminActor), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(CalendarGuards.PastDay);
        Meetings.Verify(r => r.DeleteAsync(It.IsAny<Meeting>(), It.IsAny<CancellationToken>()), Times.Never);
        Sent.Should().BeEmpty();
    }
}
