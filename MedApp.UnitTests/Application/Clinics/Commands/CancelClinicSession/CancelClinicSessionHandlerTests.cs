using AutoFixture;
using FluentAssertions;
using FluentValidation.TestHelper;
using MedApp.Application.Clinics.Commands.CancelClinicSession;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Domain.Clinics;
using MedApp.Domain.Notifications;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.ClinicsTestConstants;

namespace MedApp.UnitTests.Application.Clinics.Commands.CancelClinicSession;

[TestFixture]
public sealed class CancelClinicSessionHandlerTests : ClinicHandlerTestBase
{
    private CancelClinicSessionHandler _handler = null!;
    private ClinicSession _session = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = Fixture.Create<CancelClinicSessionHandler>();
        _session = GivenSession(CreateSession());
    }

    private CancelClinicSessionCommand Command(DateTime? expected = null) =>
        new(_session.Id, $"  {CancelReason}  ", expected ?? _session.LastUpdated, AdminActor);

    [Test]
    public async Task Handle_CancelsTheSessionAndItsBookedAppointments_ListingWhomToTell()
    {
        var first = InSession(_session, At(NextMonday, 9, 20));
        var second = InSession(_session, At(NextMonday, 9));
        Clinics.Setup(r => r.GetSessionAppointmentsForUpdateAsync(_session.Id, Token)).ReturnsAsync([first, second]);

        var response = await _handler.Handle(Command(), Token);

        _session.Status.Should().Be(ClinicSessionStatus.Cancelled);
        _session.CancelReason.Should().Be(CancelReason);
        _session.CancelledAtUtc.Should().Be(Now);
        _session.CancelledByUserId.Should().Be(AdminUserId);
        first.Status.Should().Be(AppointmentStatus.Cancelled);
        first.CancelReason.Should().Be(CancelReason);
        second.Status.Should().Be(AppointmentStatus.Cancelled);
        var events = Saved.Should().ContainSingle().Subject;
        events.Should().HaveCount(2).And.OnlyContain(e => e.Action == AppointmentEventAction.Cancelled);
        Lock.LockedOnce.Should().BeEquivalentTo([StaffUserId, _session.RoomId]);
        response.PatientsToTell.Select(p => p.AppointmentId).Should().Equal(second.Id, first.Id);
        response.PatientsToTell[0].PatientName.Should().Be($"{PatientFirstName} {PatientLastName}");
        response.Session.Status.Should().Be("Cancelled");
        var view = Activity.Views.Should().ContainSingle().Subject;
        view.Action.Should().Be(ActivityAction.PatientListed);
        view.PatientIds.Should().BeEquivalentTo([first.PatientId, second.PatientId]);
    }

    [Test]
    public async Task Handle_AttendedAppointmentsStay()
    {
        var attended = InSession(_session, At(NextMonday, 9), AppointmentStatus.Attended);
        Clinics.Setup(r => r.GetSessionAppointmentsForUpdateAsync(_session.Id, Token)).ReturnsAsync([attended]);

        var response = await _handler.Handle(Command(), Token);

        attended.Status.Should().Be(AppointmentStatus.Attended);
        response.PatientsToTell.Should().BeEmpty();
        response.Session.BookedCount.Should().Be(1);
    }

    [Test]
    public async Task Handle_SomeoneElsesSession_TellsTheClinicianOnce()
    {
        Clinics.Setup(r => r.GetSessionAppointmentsForUpdateAsync(_session.Id, Token))
            .ReturnsAsync([InSession(_session, At(NextMonday, 9)), InSession(_session, At(NextMonday, 9, 20))]);

        await _handler.Handle(Command(), Token);

        var notification = Sent.Should().ContainSingle().Subject;
        notification.Kind.Should().Be(NotificationKind.ClinicSessionCancelled);
        notification.UserId.Should().Be(StaffUserId);
        notification.Message.Should().Be($"{AdminUsername} cancelled your clinic session in Room 2, Monday 23 November at 09:00.");
        notification.CalendarDate.Should().Be(NextMonday);
    }

    [Test]
    public async Task Handle_TheirOwnSession_TellsNobody()
    {
        await _handler.Handle(Command() with { Actor = StaffActor }, Token);

        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Stale_Is409()
    {
        var act = () => _handler.Handle(Command(_session.LastUpdated.AddSeconds(1)), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ClinicRules.ChangedMeanwhile);
        _session.Status.Should().Be(ClinicSessionStatus.Planned);
    }

    [Test]
    public async Task Handle_PastDay_Is409()
    {
        var past = GivenSession(CreateSession(At(Today.AddDays(-7), 9), At(Today.AddDays(-7), 12), id: OtherSessionId));

        var act = () => _handler.Handle(new CancelClinicSessionCommand(past.Id, CancelReason, past.LastUpdated, AdminActor), Token);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public void Validator_NeedsAReasonAndTheVersion()
    {
        var result = new CancelClinicSessionValidator().TestValidate(new CancelClinicSessionCommand(_session.Id, " ", null, AdminActor));

        result.ShouldHaveValidationErrorFor(x => x.Reason);
        result.ShouldHaveValidationErrorFor(x => x.ExpectedLastUpdated);
        new CancelClinicSessionValidator().TestValidate(new CancelClinicSessionCommand(_session.Id, new string('x', 501), Now, AdminActor))
            .ShouldHaveValidationErrorFor(x => x.Reason);
    }
}
