using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetAvailability;
using MedApp.Application.Common.Exceptions;
using MedApp.UnitTests.Application.Calendar.Meetings;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Queries.GetAvailability;

[TestFixture]
public sealed class GetAvailabilityHandlerTests : MeetingHandlerTestBase
{
    private static readonly DateOnly Tuesday = NextMonday.AddDays(1);

    private GetAvailabilityHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<GetAvailabilityHandler>();

    private static GetAvailabilityQuery Query(
        IReadOnlyList<Guid>? staff = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int durationMinutes = 60,
        Guid? exceptMeetingId = null) =>
        new(staff ?? [StaffUserId, OtherStaffUserId], from ?? NextMonday, to ?? Tuesday, durationMinutes, exceptMeetingId);

    [Test]
    public async Task Handle_Range_ReadsEveryonesCalendarsOverIt()
    {
        var response = await _handler.Handle(Query(), Token);

        response.From.Should().Be(NextMonday);
        response.To.Should().Be(Tuesday);
        response.DurationMinutes.Should().Be(60);
        var read = CalendarReads.Should().ContainSingle().Subject;
        read.StaffUserIds.Should().Equal(StaffUserId, OtherStaffUserId);
        read.From.Should().Be(NextMonday);
        read.To.Should().Be(Tuesday);
        read.Token.Should().Be(Token);
        Identity.Verify(i => i.GetStaffAsync(Token), Times.Once);
    }

    [Test]
    public async Task Handle_SomeoneWhoIsntStaff_ThrowsNotFoundWithoutReading()
    {
        var stranger = Guid.Parse("0000000d-0000-0000-0000-0000000000ff");

        var act = () => _handler.Handle(Query([StaffUserId, stranger]), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Staff member(s) not found: {stranger}.");
        CalendarReads.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Days_AreEachWorkingDaysFreeTimeForEveryone()
    {
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, Tuesday, templateBlocks: [CreateTemplateBlock(12, 13)]));
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, Tuesday, blocks: [CreateBlock(At(Tuesday, 8), At(Tuesday, 18), Leave)]));

        var response = await _handler.Handle(Query(to: NextMonday.AddDays(6)), Token);

        response.Days.Select(d => d.Date).Should().Equal(Enumerable.Range(0, 5).Select(i => NextMonday.AddDays(i)));
        response.Days[0].Free.Select(f => (f.StartUtc, f.EndUtc)).Should().Equal(
            (At(NextMonday, 8), At(NextMonday, 12)),
            (At(NextMonday, 13), At(NextMonday, 18)));
        response.Days[1].Free.Should().BeEmpty("someone is on leave");
    }

    [Test]
    public async Task Handle_Suggestions_FitTheLengthInEveryonesFreeTime()
    {
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, Tuesday, blocks: [CreateBlock(At(NextMonday, 8), At(NextMonday, 16, 30))]));

        var response = await _handler.Handle(Query(durationMinutes: 45), Token);

        response.Suggestions.Select(s => (s.StartUtc, s.EndUtc)).Should().Equal(
            (At(NextMonday, 16, 30), At(NextMonday, 17, 15)),
            (At(NextMonday, 17, 15), At(NextMonday, 18)),
            (At(Tuesday, 8), At(Tuesday, 8, 45)),
            (At(Tuesday, 8, 45), At(Tuesday, 9, 30)),
            (At(Tuesday, 9, 30), At(Tuesday, 10, 15)));
    }

    [Test]
    public async Task Handle_Busy_IsEachPersonsBusyTimesByUsernameInTimeOrder()
    {
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, Tuesday,
            templateBlocks: [CreateTemplateBlock(12, 13)],
            appointments: [CreateAppointment(startUtc: At(NextMonday, 9))]));
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, Tuesday,
            meetings: [NewMeeting(At(NextMonday, 12), At(NextMonday, 12, 30), [OtherStaffUserId])]));

        var response = await _handler.Handle(Query(), Token);

        response.Busy.Select(b => (b.Username, b.StaffUserId, b.StartUtc, b.EndUtc, b.What)).Should().Equal(
            (StaffUsername, StaffUserId, At(NextMonday, 9), At(NextMonday, 9, 30), "another appointment"),
            (OtherStaffUsername, OtherStaffUserId, At(NextMonday, 12), At(NextMonday, 12, 30), $"the meeting '{Title}'"),
            (StaffUsername, StaffUserId, At(NextMonday, 12), At(NextMonday, 13), "Lunch"),
            (StaffUsername, StaffUserId, At(Tuesday, 12), At(Tuesday, 13), "Lunch"));
        response.Busy.Should().OnlyContain(b => !b.What.Contains(PatientFirstName) && !b.What.Contains(PatientLastName));
    }

    [Test]
    public async Task Handle_ExceptAMeeting_LeavesItOutOfBusyFreeAndSuggestedTimes()
    {
        var meeting = NewMeeting(At(NextMonday, 8), At(NextMonday, 18));
        GivenCalendar(CalendarOf(StaffUserId, NextMonday, Tuesday, meetings: [meeting]));
        GivenCalendar(CalendarOf(OtherStaffUserId, NextMonday, Tuesday, meetings: [meeting]));

        var moving = await _handler.Handle(Query(to: NextMonday, exceptMeetingId: MeetingId), Token);
        var notMoving = await _handler.Handle(Query(to: NextMonday), Token);

        moving.Busy.Should().BeEmpty();
        moving.Days.Single().Free.Select(f => (f.StartUtc, f.EndUtc)).Should().Equal((At(NextMonday, 8), At(NextMonday, 18)));
        moving.Suggestions.Should().HaveCount(3);
        notMoving.Busy.Should().HaveCount(2);
        notMoving.Days.Single().Free.Should().BeEmpty();
        notMoving.Suggestions.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Today_IsFreeOnlyFromNow()
    {
        var response = await _handler.Handle(Query(from: Today, to: Today), Token);

        response.Days.Single().Free.Select(f => (f.StartUtc, f.EndUtc)).Should().Equal((Now, At(Today, 18)));
        response.Suggestions.First().StartUtc.Should().Be(Now);
    }
}
