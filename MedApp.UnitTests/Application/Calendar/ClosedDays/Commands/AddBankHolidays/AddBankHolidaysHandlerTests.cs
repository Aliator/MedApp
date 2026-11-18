using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.ClosedDays.Commands.AddBankHolidays;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Domain.Notifications;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ClosedDays.Commands.AddBankHolidays;

[TestFixture]
public sealed class AddBankHolidaysHandlerTests
{
    private Mock<IClosedDayRepository> _repository = null!;
    private AddBankHolidaysHandler _handler = null!;
    private List<ClosedDay> _existing = null!;
    private List<IReadOnlyCollection<ClosedDay>> _adds = null!;
    private Mock<ICalendarRepository> _calendar = null!;
    private Mock<IIdentityReadService> _identity = null!;
    private Dictionary<DateOnly, List<StaffBookings>> _bookings = null!;
    private List<Notification> _sent = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IClosedDayRepository>();
        _existing = [];
        _adds = [];
        _repository.Setup(r => r.GetAsync(It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateOnly? from, DateOnly? to, CancellationToken _) =>
                (IReadOnlyList<ClosedDay>)_existing.Where(d => d.Date >= from && d.Date <= to).ToList());
        _repository.Setup(r => r.AddAsync(It.IsAny<IReadOnlyCollection<ClosedDay>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<ClosedDay> days, CancellationToken _) => _adds.Add(days))
            .Returns(Task.CompletedTask);
        _calendar = new Mock<ICalendarRepository>();
        _bookings = [];
        _calendar.Setup(c => c.GetBookingsOnAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateOnly date, CancellationToken _) => (IReadOnlyList<StaffBookings>)(_bookings.GetValueOrDefault(date) ?? []));
        _identity = new Mock<IIdentityReadService>();
        _identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [StaffUserId] = StaffUsername, [OtherStaffUserId] = OtherStaffUsername });
        _sent = [];
        var sender = new Mock<INotificationSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification n, CancellationToken _) => _sent.Add(n))
            .Returns(Task.CompletedTask);
        fixture.Inject(_calendar.Object);
        fixture.Inject(_identity.Object);
        fixture.Inject(sender.Object);
        fixture.Inject(_repository.Object);
        fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
        _handler = fixture.Create<AddBankHolidaysHandler>();
    }

    [Test]
    public async Task Handle_NextYear_AddsAllEightInOneSave()
    {
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(new AddBankHolidaysCommand(2027, AdminActor), cts.Token);

        var added = _adds.Should().ContainSingle().Subject;
        added.Select(d => d.Date).Should().Equal(
            new DateOnly(2027, 1, 1), new DateOnly(2027, 3, 26), new DateOnly(2027, 3, 29), new DateOnly(2027, 5, 3),
            new DateOnly(2027, 5, 31), new DateOnly(2027, 8, 30), new DateOnly(2027, 12, 27), new DateOnly(2027, 12, 28));
        added.Should().OnlyContain(d => d.CreatedByUserId == AdminUserId && d.CreatedAtUtc == Now);
        added.Last().Name.Should().Be("Boxing Day (substitute day)");
        _repository.Verify(r => r.GetAsync(new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 28), cts.Token), Times.Once);
        _repository.Verify(r => r.AddAsync(It.IsAny<IReadOnlyCollection<ClosedDay>>(), cts.Token), Times.Once);

        response.Year.Should().Be(2027);
        response.Added.Select(d => d.Date).Should().Equal(added.Select(d => d.Date));
        response.Added.Should().OnlyContain(d => !d.IsPast);
        response.Skipped.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_DatesAlreadyClosed_SkipsThem()
    {
        // Christmas Day 2027 is on a Saturday: an admin closed the 27th by hand as something else.
        _existing.Add(CalendarTestConstants.CreateClosedDay(new DateOnly(2027, 12, 27), "Clinic closed"));

        var response = await _handler.Handle(new AddBankHolidaysCommand(2027, AdminActor), CancellationToken.None);

        _adds.Single().Select(d => d.Date).Should().NotContain(new DateOnly(2027, 12, 27)).And.HaveCount(7);
        var skipped = response.Skipped.Should().ContainSingle().Subject;
        skipped.Date.Should().Be(new DateOnly(2027, 12, 27));
        skipped.Name.Should().Be("Christmas Day (substitute day)");
        skipped.Reason.Should().Be(AddBankHolidaysHandler.AlreadyClosed);
    }

    [Test]
    public async Task Handle_ThisYear_SkipsThosePast()
    {
        // The clock is Monday 16 November 2026: only Christmas Day and Boxing Day (substitute) are still to come.
        var response = await _handler.Handle(new AddBankHolidaysCommand(2026, AdminActor), CancellationToken.None);

        response.Added.Select(d => d.Date).Should().Equal(new DateOnly(2026, 12, 25), new DateOnly(2026, 12, 28));
        response.Skipped.Should().HaveCount(6).And.OnlyContain(s => s.Reason == AddBankHolidaysHandler.Past);
    }

    [Test]
    public async Task Handle_EveryOneAlreadyClosed_SavesNothing()
    {
        await _handler.Handle(new AddBankHolidaysCommand(2027, AdminActor), CancellationToken.None);
        _existing.AddRange(_adds.Single());

        var response = await _handler.Handle(new AddBankHolidaysCommand(2027, AdminActor), CancellationToken.None);

        _adds.Should().ContainSingle("the second time had nothing to add");
        response.Added.Should().BeEmpty();
        response.Skipped.Should().HaveCount(8).And.OnlyContain(s => s.Reason == AddBankHolidaysHandler.AlreadyClosed);
    }

    [Test]
    public async Task Handle_AddedDaysWithBookings_SayWhatsOnEach_AndTellTheStaff()
    {
        var christmas = new DateOnly(2027, 12, 27);
        _bookings[christmas] = [new StaffBookings(OtherStaffUserId, Appointments: 1, Blocks: 2, Meetings: 0)];

        var response = await _handler.Handle(new AddBankHolidaysCommand(2027, AdminActor), CancellationToken.None);

        response.Added.Single(d => d.Date == christmas).Bookings!.Should().BeEquivalentTo(new { Appointments = 1, Blocks = 2, Meetings = 0 });
        response.Added.Where(d => d.Date != christmas).Should().OnlyContain(d => d.Bookings!.Staff.Count == 0);
        var notification = _sent.Should().ContainSingle().Subject;
        notification.UserId.Should().Be(OtherStaffUserId);
        notification.Message.Should().Contain("Monday 27 December (Christmas Day (substitute day))").And.Contain("an appointment and 2 blocks");
    }
}
