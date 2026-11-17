using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendar;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Queries.GetCalendar;

[TestFixture]
public sealed class GetCalendarHandlerTests : StaffCalendarHandlerTestBase
{
    private GetCalendarHandler _handler = null!;
    private Appointment _appointment = null!;
    private List<Guid> _lookedUp = null!;

    [SetUp]
    public void SetUp()
    {
        _appointment = CreateAppointment(startUtc: At(NextMonday, 10));
        _appointment.BookedByUserId = OtherStaffUserId;
        GivenCalendar(CreateData(NextMonday, NextFriday,
            templateBlocks: [CreateTemplateBlock(12, 13)],
            blocks: [CreateBlock(At(NextMonday, 9), At(NextMonday, 9, 30))],
            appointments: [_appointment]));

        var defaultWeek = CreateStaffWeek();
        defaultWeek.WeekTemplate = CreateWeekTemplate();
        Repository
            .Setup(r => r.GetStaffWeekTemplateOnAsync(StaffUserId, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultWeek);

        _lookedUp = [];
        Identity
            .Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<Guid> ids, CancellationToken _) => _lookedUp.AddRange(ids))
            .ReturnsAsync(new Dictionary<Guid, string> { [StaffUserId] = StaffUsername });

        _handler = Fixture.Create<GetCalendarHandler>();
    }

    [Test]
    public async Task Handle_NoStaffMemberGiven_ReturnsTheActorsOwnCalendar()
    {
        var response = await _handler.Handle(new GetCalendarQuery(null, NextMonday, NextFriday, StaffActor), Token);

        Repository.Verify(r => r.GetCalendarDataAsync(StaffUserId, NextMonday, NextFriday, Token), Times.Once);
        Repository.Verify(r => r.GetStaffWeekTemplateOnAsync(StaffUserId, Today, Token), Times.Once);
        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);
        Identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);

        response.StaffUserId.Should().Be(StaffUserId);
        response.StaffUsername.Should().Be(StaffUsername);
        response.From.Should().Be(NextMonday);
        response.To.Should().Be(NextFriday);
        response.CanEdit.Should().BeTrue();
        response.DefaultWeekTemplateId.Should().Be(WeekTemplateId);
        response.DefaultWeekTemplateName.Should().Be(WeekTemplateName);
        response.Days.Should().HaveCount(5);
        response.Blocks.Should().HaveCount(6, "five lunches from the template and one stored block");
        response.Blocks.Should().OnlyContain(b => b.CanEdit);
        response.Appointments.Should().ContainSingle().Which.Id.Should().Be(_appointment.Id);
    }

    [Test]
    public async Task Handle_AppointmentUsers_AreLookedUpOnceEachWithDeletedOnesNamedSo()
    {
        var response = await _handler.Handle(new GetCalendarQuery(StaffUserId, NextMonday, NextFriday, StaffActor), Token);

        _lookedUp.Should().BeEquivalentTo([StaffUserId, OtherStaffUserId]);
        var appointment = response.Appointments.Single();
        appointment.StaffUsername.Should().Be(StaffUsername);
        appointment.BookedByUsername.Should().Be(ProcedureMapper.DeletedUser);
    }

    /// <summary>The calendar also has a meeting the admin arranged for its owner and the other member of staff.</summary>
    private Meeting GivenAMeeting()
    {
        var meeting = MeetingsTestConstants.NewMeeting(At(NextMonday, 15), At(NextMonday, 16));
        GivenCalendar(CreateData(NextMonday, NextFriday, appointments: [_appointment]) with { Meetings = [meeting] });
        Identity
            .Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<Guid> ids, CancellationToken _) => _lookedUp.AddRange(ids))
            .ReturnsAsync(MeetingsTestConstants.Usernames);
        return meeting;
    }

    [Test]
    public async Task Handle_Meetings_AreReturnedWithTheirPeopleLookedUpOnce()
    {
        var meeting = GivenAMeeting();

        var response = await _handler.Handle(new GetCalendarQuery(StaffUserId, NextMonday, NextFriday, StaffActor), Token);

        _lookedUp.Should().BeEquivalentTo([StaffUserId, OtherStaffUserId, AdminUserId]);
        var returned = response.Meetings.Should().ContainSingle().Subject;
        returned.Id.Should().Be(meeting.Id);
        returned.OrganiserUsername.Should().Be(AdminUsername);
        returned.Attendees.Select(a => a.Username).Should().Equal(OtherStaffUsername, StaffUsername);
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public async Task Handle_Meetings_OnlyAnAdminCanChangeThem(bool isAdmin, bool canEdit)
    {
        GivenAMeeting();

        var response = await _handler.Handle(
            new GetCalendarQuery(StaffUserId, NextMonday, NextFriday, StaffActor with { IsAdmin = isAdmin }), Token);

        response.CanEdit.Should().BeTrue("it's their own calendar");
        response.Meetings.Single().CanEdit.Should().Be(canEdit);
    }

    [Test]
    public async Task Handle_SomeoneElsesCalendarAsStaff_CanBeReadButNotEdited()
    {
        var response = await _handler.Handle(new GetCalendarQuery(StaffUserId, NextMonday, NextFriday, OtherStaffActor), Token);

        response.StaffUserId.Should().Be(StaffUserId);
        response.CanEdit.Should().BeFalse();
        response.Blocks.Should().OnlyContain(b => !b.CanEdit);
    }

    [Test]
    public async Task Handle_SomeoneElsesCalendarAsAdmin_CanBeEdited()
    {
        var response = await _handler.Handle(new GetCalendarQuery(StaffUserId, NextMonday, NextFriday, AdminActor), Token);

        response.CanEdit.Should().BeTrue();
    }

    [Test]
    public async Task Handle_NoDefaultWeek_LeavesItEmpty()
    {
        Repository
            .Setup(r => r.GetStaffWeekTemplateOnAsync(StaffUserId, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StaffWeekTemplate?)null);

        var response = await _handler.Handle(new GetCalendarQuery(null, NextMonday, NextFriday, StaffActor), Token);

        response.DefaultWeekTemplateId.Should().BeNull();
        response.DefaultWeekTemplateName.Should().BeNull();
    }

    [Test]
    public async Task Handle_NotAMemberOfStaff_ThrowsNotFound()
    {
        var stranger = Guid.NewGuid();

        var act = () => _handler.Handle(new GetCalendarQuery(stranger, NextMonday, NextFriday, StaffActor), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Staff member '{stranger}' was not found.");
        Repository.Verify(r => r.GetCalendarDataAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_WithAppointments_RecordsTheirPatientsAsListed()
    {
        var activity = new RecordingActivityLog();
        Fixture.Inject<IActivityLog>(activity);

        await Fixture.Create<GetCalendarHandler>().Handle(new GetCalendarQuery(null, NextMonday, NextFriday, StaffActor), Token);

        var (action, ids, procedureId) = activity.Views.Should().ContainSingle().Subject;
        action.Should().Be(ActivityAction.PatientListed);
        ids.Should().Equal(_appointment.PatientId);
        procedureId.Should().BeNull();
        activity.Tokens.Should().Equal(Token);
    }
}
