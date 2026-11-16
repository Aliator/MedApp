using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class MeetingStaffTests
{
    private static readonly Guid Stranger = Guid.Parse("0000000d-0000-0000-0000-0000000000ff");
    private static readonly Guid OtherStranger = Guid.Parse("0000000d-0000-0000-0000-0000000000fe");

    private Mock<IIdentityReadService> _identity = null!;
    private Mock<ICalendarRepository> _calendars = null!;
    private Dictionary<Guid, CalendarData> _data = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _cts = new CancellationTokenSource();
        _identity = new Mock<IIdentityReadService>();
        _identity.Setup(i => i.GetStaffAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Staff);

        _data = [];
        _calendars = new Mock<ICalendarRepository>();
        _calendars
            .Setup(r => r.GetCalendarsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, DateOnly from, DateOnly to, CancellationToken _) =>
                (IReadOnlyList<CalendarData>)ids.Select(id => _data.GetValueOrDefault(id) ?? CalendarOf(id, from, to)).ToList());
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    // GetStaffOrThrowAsync

    [Test]
    public async Task GetStaffOrThrowAsync_AllStaff_ReturnsEachOnceInTheStaffOrder()
    {
        var staff = await _identity.Object.GetStaffOrThrowAsync([AdminUserId, StaffUserId, AdminUserId], _cts.Token);

        staff.Should().Equal((StaffUserId, StaffUsername), (AdminUserId, AdminUsername));
        _identity.Verify(i => i.GetStaffAsync(_cts.Token), Times.Once);
    }

    [Test]
    public async Task GetStaffOrThrowAsync_SomeArentStaff_ThrowsNotFoundNamingEachOnce()
    {
        var act = () => _identity.Object.GetStaffOrThrowAsync([StaffUserId, Stranger, OtherStranger, Stranger], _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Staff member(s) not found: {Stranger}, {OtherStranger}.");
    }

    [Test]
    public async Task GetStaffOrThrowAsync_NoneAsked_ReturnsNone()
    {
        (await _identity.Object.GetStaffOrThrowAsync([], _cts.Token)).Should().BeEmpty();
    }

    // EnsureEveryoneFreeAsync

    private static readonly IReadOnlyList<(Guid UserId, string Username)> Attendees =
        [(StaffUserId, StaffUsername), (OtherStaffUserId, OtherStaffUsername)];

    [Test]
    public async Task EnsureEveryoneFreeAsync_EveryoneFree_ReadsTheirCalendarsThatDayAndPasses()
    {
        var act = () => _calendars.Object.EnsureEveryoneFreeAsync(Attendees, At(NextMonday, 10), At(NextMonday, 11), null, _cts.Token);

        await act.Should().NotThrowAsync();
        _calendars.Verify(r => r.GetCalendarsAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { StaffUserId, OtherStaffUserId })),
            NextMonday,
            NextMonday,
            _cts.Token), Times.Once);
    }

    [Test]
    public async Task EnsureEveryoneFreeAsync_SomeoneHasABlock_ThrowsConflictNamingThemAndIt()
    {
        _data[OtherStaffUserId] = CalendarOf(OtherStaffUserId, NextMonday, templateBlocks: [CreateTemplateBlock(12, 13)]);

        var act = () => _calendars.Object.EnsureEveryoneFreeAsync(Attendees, At(NextMonday, 11, 30), At(NextMonday, 12, 30), null, _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{OtherStaffUsername} has Lunch, 12:00 to 13:00.");
    }

    [Test]
    public async Task EnsureEveryoneFreeAsync_SomeoneHasAnAppointment_NamesNoPatient()
    {
        _data[StaffUserId] = CalendarOf(StaffUserId, NextMonday, appointments: [CreateAppointment(startUtc: At(NextMonday, 10))]);

        var act = () => _calendars.Object.EnsureEveryoneFreeAsync(Attendees, At(NextMonday, 10), At(NextMonday, 11), null, _cts.Token);

        (await act.Should().ThrowAsync<ConflictException>())
            .WithMessage($"{StaffUsername} has another appointment, 10:00 to 10:30.")
            .Which.Message.Should().NotContain(PatientFirstName).And.NotContain(PatientLastName);
    }

    [Test]
    public async Task EnsureEveryoneFreeAsync_SomeoneInAnotherMeeting_NamesTheMeeting()
    {
        _data[StaffUserId] = CalendarOf(StaffUserId, NextMonday,
            meetings: [NewMeeting(At(NextMonday, 9), At(NextMonday, 10, 15), [StaffUserId], OtherMeetingId, OtherTitle)]);

        var act = () => _calendars.Object.EnsureEveryoneFreeAsync(Attendees, At(NextMonday, 10), At(NextMonday, 11), MeetingId, _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{StaffUsername} has the meeting '{OtherTitle}', 09:00 to 10:15.");
    }

    [Test]
    public async Task EnsureEveryoneFreeAsync_TheMeetingBeingMoved_IsNotAClash()
    {
        var meeting = NewMeeting(At(NextMonday, 10), At(NextMonday, 11));
        _data[StaffUserId] = CalendarOf(StaffUserId, NextMonday, meetings: [meeting]);
        _data[OtherStaffUserId] = CalendarOf(OtherStaffUserId, NextMonday, meetings: [meeting]);

        var act = () => _calendars.Object.EnsureEveryoneFreeAsync(Attendees, At(NextMonday, 10, 30), At(NextMonday, 11, 30), MeetingId, _cts.Token);

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task EnsureEveryoneFreeAsync_SeveralBusy_NamesTheFirstAttendeeGiven()
    {
        _data[StaffUserId] = CalendarOf(StaffUserId, NextMonday, blocks: [CreateBlock(At(NextMonday, 10), At(NextMonday, 11))]);
        _data[OtherStaffUserId] = CalendarOf(OtherStaffUserId, NextMonday, blocks: [CreateBlock(At(NextMonday, 9), At(NextMonday, 10, 30))]);

        var act = () => _calendars.Object.EnsureEveryoneFreeAsync(Attendees, At(NextMonday, 10), At(NextMonday, 11), null, _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{StaffUsername} has Training, 10:00 to 11:00.");
    }

    [Test]
    public async Task EnsureEveryoneFreeAsync_InSummer_DescribesTheUkTime()
    {
        _data[StaffUserId] = CalendarOf(StaffUserId, SummerMonday, templateBlocks: [CreateTemplateBlock(12, 13)]);

        var act = () => _calendars.Object.EnsureEveryoneFreeAsync(Attendees, At(SummerMonday, 12), At(SummerMonday, 12, 30), null, _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{StaffUsername} has Lunch, 12:00 to 13:00.");
        _calendars.Verify(r => r.GetCalendarsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), SummerMonday, SummerMonday, _cts.Token), Times.Once);
    }

    // Attendees

    [Test]
    public void Attendees_StaffWithRepeats_AreEachStoredOnceForTheMeeting()
    {
        var attendees = MeetingStaff.Attendees(MeetingId, [OtherStaffUserId, StaffUserId, OtherStaffUserId]);

        attendees.Select(a => a.StaffUserId).Should().Equal(OtherStaffUserId, StaffUserId);
        attendees.Should().OnlyContain(a => a.MeetingId == MeetingId);
    }
}
