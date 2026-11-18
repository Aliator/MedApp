using AutoFixture;
using FluentAssertions;
using FluentValidation.TestHelper;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Clinics.Queries.GetClinicPatterns;
using MedApp.Application.Clinics.Queries.GetClinicSessionById;
using MedApp.Application.Clinics.Queries.SearchClinicSessions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Activity;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.ClinicsTestConstants;

namespace MedApp.UnitTests.Application.Clinics.Queries;

[TestFixture]
public sealed class ClinicQueryHandlerTests : ClinicHandlerTestBase
{
    [Test]
    public async Task Search_PassesTheFilter_AndCountsEachSessionsSlots()
    {
        var session = CreateSession();
        ClinicSessionFilter? filter = null;
        Clinics.Setup(r => r.SearchSessionsAsync(It.IsAny<ClinicSessionFilter>(), Token))
            .Callback((ClinicSessionFilter f, CancellationToken _) => filter = f)
            .ReturnsAsync([session]);
        Clinics.Setup(r => r.GetBookedStartsAsync(It.IsAny<IEnumerable<Guid>>(), Token))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<DateTime>> { [session.Id] = [At(NextMonday, 9)] });

        var result = await Fixture.Create<SearchClinicSessionsHandler>().Handle(
            new SearchClinicSessionsQuery(NextMonday, NextFriday, StaffUserId, null, null, SpecialtyId, true, false, StaffActor), Token);

        filter.Should().Be(new ClinicSessionFilter(NextMonday, NextFriday, StaffUserId, null, null, SpecialtyId, true));
        var response = result.Should().ContainSingle().Subject;
        response.BookedCount.Should().Be(1);
        response.FreeSlotCount.Should().Be(8);
        response.CanChange.Should().BeFalse("all staff before roles don't manage clinics");
        Activity.Views.Should().BeEmpty();
    }

    [Test]
    public async Task Search_WithFreeSlots_LeavesOutFullSessions_AndCancelledOnes()
    {
        var full = CreateSession(At(NextMonday, 9), At(NextMonday, 9, 40));
        var open = CreateSession(At(NextMonday, 13), At(NextMonday, 14), id: OtherSessionId);
        ClinicSessionFilter? filter = null;
        Clinics.Setup(r => r.SearchSessionsAsync(It.IsAny<ClinicSessionFilter>(), Token))
            .Callback((ClinicSessionFilter f, CancellationToken _) => filter = f)
            .ReturnsAsync([full, open]);
        Clinics.Setup(r => r.GetBookedStartsAsync(It.IsAny<IEnumerable<Guid>>(), Token))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<DateTime>> { [full.Id] = [At(NextMonday, 9), At(NextMonday, 9, 20)] });

        var result = await Fixture.Create<SearchClinicSessionsHandler>().Handle(
            new SearchClinicSessionsQuery(NextMonday, NextFriday, null, null, null, null, true, true, AdminActor), Token);

        result.Select(s => s.Id).Should().Equal(open.Id);
        filter!.IncludeCancelled.Should().BeFalse();
    }

    [Test]
    public void Search_Validator_LimitsTheRange()
    {
        var validator = new SearchClinicSessionsValidator();

        validator.TestValidate(new SearchClinicSessionsQuery(NextMonday, NextMonday.AddDays(61), null, null, null, null, false, false, AdminActor))
            .ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new SearchClinicSessionsQuery(NextMonday, NextMonday.AddDays(62), null, null, null, null, false, false, AdminActor))
            .ShouldHaveValidationErrorFor(x => x.To);
        validator.TestValidate(new SearchClinicSessionsQuery(NextMonday, Today, null, null, null, null, false, false, AdminActor))
            .ShouldHaveValidationErrorFor(x => x.To);
    }

    [Test]
    public async Task GetById_ListsEachSlotWithWhoIsInIt_AndRecordsTheListing()
    {
        var session = GivenSession(CreateSession());
        var booked = InSession(session, At(NextMonday, 9, 20));
        Clinics.Setup(r => r.GetSessionAppointmentsAsync(session.Id, Token)).ReturnsAsync([booked]);

        var detail = await Fixture.Create<GetClinicSessionByIdHandler>().Handle(new GetClinicSessionByIdQuery(session.Id, AdminActor), Token);

        detail.Slots.Should().HaveCount(9);
        detail.Slots[1].AppointmentId.Should().Be(booked.Id);
        detail.Slots[1].PatientName.Should().Be($"{PatientFirstName} {PatientLastName}");
        detail.Slots[0].AppointmentId.Should().BeNull();
        detail.Session.CanChange.Should().BeTrue();
        Activity.Views.Should().ContainSingle().Which.Action.Should().Be(ActivityAction.PatientListed);
        Activity.Views.Single().PatientIds.Should().Equal(booked.PatientId);
    }

    [Test]
    public async Task GetById_Unknown_Is404()
    {
        var act = () => Fixture.Create<GetClinicSessionByIdHandler>().Handle(new GetClinicSessionByIdQuery(Guid.NewGuid(), AdminActor), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task GetPatterns_MapsEachWithItsClinician()
    {
        Clinics.Setup(r => r.GetPatternsAsync(Token)).ReturnsAsync([CreatePattern(untilDate: Today.AddDays(-1), fromDate: LongAgo)]);

        var patterns = await Fixture.Create<GetClinicPatternsHandler>().Handle(new GetClinicPatternsQuery(), Token);

        var pattern = patterns.Should().ContainSingle().Subject;
        pattern.StaffUsername.Should().Be(StaffUsername);
        pattern.DayOfWeek.Should().Be("Monday");
        pattern.HasEnded.Should().BeTrue();
        pattern.SiteName.Should().Be(MedApp.UnitTests.Common.Constants.SitesTestConstants.SiteName);
        pattern.SpecialtyName.Should().Be(SpecialtyName);
    }
}
