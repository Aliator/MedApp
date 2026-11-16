using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.Meetings.Queries.GetMeetingById;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Meetings.Queries.GetMeetingById;

[TestFixture]
public sealed class GetMeetingByIdHandlerTests : MeetingHandlerTestBase
{
    private GetMeetingByIdHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<GetMeetingByIdHandler>();

    private void GivenMeeting(Meeting meeting) =>
        Meetings.Setup(r => r.GetAsync(meeting.Id, It.IsAny<CancellationToken>())).ReturnsAsync(meeting);

    [Test]
    public async Task Handle_Found_ReturnsItWithItsPeopleByUsername()
    {
        GivenMeeting(NewMeeting(At(NextMonday, 10), At(NextMonday, 11)));

        var response = await _handler.Handle(new GetMeetingByIdQuery(MeetingId, StaffActor), Token);

        response.Id.Should().Be(MeetingId);
        response.Title.Should().Be(Title);
        response.OrganiserUsername.Should().Be(AdminUsername);
        response.Attendees.Select(a => a.Username).Should().Equal(OtherStaffUsername, StaffUsername);
        Meetings.Verify(r => r.GetAsync(MeetingId, Token), Times.Once);
        Identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public async Task Handle_UpcomingMeeting_OnlyAnAdminCanChangeIt(bool isAdmin, bool canEdit)
    {
        GivenMeeting(NewMeeting(At(NextMonday, 10), At(NextMonday, 11)));

        var response = await _handler.Handle(new GetMeetingByIdQuery(MeetingId, StaffActor with { IsAdmin = isAdmin }), Token);

        response.CanEdit.Should().Be(canEdit);
    }

    [Test]
    public async Task Handle_MeetingOnAPastDay_CantBeChangedEvenByAnAdmin()
    {
        var friday = Today.AddDays(-3);
        GivenMeeting(NewMeeting(At(friday, 10), At(friday, 11)));

        var response = await _handler.Handle(new GetMeetingByIdQuery(MeetingId, AdminActor), Token);

        response.CanEdit.Should().BeFalse();
    }

    [Test]
    public async Task Handle_NotFound_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new GetMeetingByIdQuery(OtherMeetingId, StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Meeting '{OtherMeetingId}' was not found.");
    }
}
