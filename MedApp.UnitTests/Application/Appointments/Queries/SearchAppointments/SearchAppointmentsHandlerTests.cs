using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Queries.SearchAppointments;
using MedApp.Application.Common.Paging;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Queries.SearchAppointments;

[TestFixture]
public sealed class SearchAppointmentsHandlerTests : AppointmentHandlerTestBase
{
    private SearchAppointmentsHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<SearchAppointmentsHandler>();

    [Test]
    public async Task Handle_Page_MapsItsAppointmentsLookingUpUsernamesOnce()
    {
        var query = AppointmentsTestConstants.CreateSearchQuery() with { Page = 2, PageSize = 2 };
        var booked = CreateAppointment(AppointmentStatus.Booked);
        var requested = CreateAppointment(AppointmentStatus.Requested);
        using var cts = new CancellationTokenSource();
        Repository.Setup(r => r.SearchAsync(query, cts.Token))
            .ReturnsAsync(new PagedResult<Appointment>([booked, requested], 5));

        var response = await _handler.Handle(query, cts.Token);

        response.Items.Select(i => i.Id).Should().Equal(booked.Id, requested.Id);
        response.Items[0].StaffUsername.Should().Be(StaffUsername);
        response.Items[1].StaffUsername.Should().BeNull();
        response.Page.Should().Be(2);
        response.PageSize.Should().Be(2);
        response.TotalCount.Should().Be(5);
        response.TotalPages.Should().Be(3);
        Identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_NoMatches_ReturnsAnEmptyPage()
    {
        var query = AppointmentsTestConstants.CreateSearchQuery();
        Repository.Setup(r => r.SearchAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Appointment>([], 0));

        var response = await _handler.Handle(query, CancellationToken.None);

        response.Items.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }

    [Test]
    public async Task Handle_Page_RecordsEachPatientOnItAsListed()
    {
        var activity = new RecordingActivityLog();
        Fixture.Inject<IActivityLog>(activity);
        var query = AppointmentsTestConstants.CreateSearchQuery();
        var first = CreateAppointment(AppointmentStatus.Booked);
        var second = CreateAppointment(AppointmentStatus.Requested);
        using var cts = new CancellationTokenSource();
        Repository.Setup(r => r.SearchAsync(query, cts.Token))
            .ReturnsAsync(new PagedResult<Appointment>([first, second], 2));

        await Fixture.Create<SearchAppointmentsHandler>().Handle(query, cts.Token);

        var (action, ids, pathwayId) = activity.Views.Should().ContainSingle().Subject;
        action.Should().Be(ActivityAction.PatientListed);
        ids.Should().Equal(first.PatientId, second.PatientId);
        pathwayId.Should().BeNull();
        activity.Tokens.Should().Equal(cts.Token);
    }
}
