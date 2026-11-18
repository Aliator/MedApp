using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.ClosedDays.Queries.GetClosedDayBookings;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ClosedDays.Queries.GetClosedDayBookings;

[TestFixture]
public sealed class GetClosedDayBookingsTests
{
    [Test]
    public async Task Handle_ReturnsTheDaysBookings()
    {
        var fixture = AutoFixtureFactory.Create();
        var calendar = new Mock<ICalendarRepository>();
        var identity = new Mock<IIdentityReadService>();
        using var cts = new CancellationTokenSource();
        calendar.Setup(c => c.GetBookingsOnAsync(ClosedWednesday, cts.Token)).ReturnsAsync([new StaffBookings(StaffUserId, 3, 0, 0)]);
        identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token))
            .ReturnsAsync(new Dictionary<Guid, string> { [StaffUserId] = StaffUsername });
        fixture.Inject(calendar.Object);
        fixture.Inject(identity.Object);

        var response = await fixture.Create<GetClosedDayBookingsHandler>().Handle(new GetClosedDayBookingsQuery(ClosedWednesday), cts.Token);

        response.Appointments.Should().Be(3);
        response.Staff.Should().ContainSingle().Which.Username.Should().Be(StaffUsername);
    }

    [Test]
    public void Validate_NoDate_Fails()
    {
        new GetClosedDayBookingsValidator().Validate(new GetClosedDayBookingsQuery(default)).IsValid.Should().BeFalse();
        new GetClosedDayBookingsValidator().Validate(new GetClosedDayBookingsQuery(ClosedWednesday)).IsValid.Should().BeTrue();
    }
}
