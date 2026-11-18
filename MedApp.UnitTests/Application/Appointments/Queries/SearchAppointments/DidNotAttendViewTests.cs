using FluentAssertions;
using MedApp.Application.Appointments.Queries.SearchAppointments;
using MedApp.Domain.Appointments;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Queries.SearchAppointments;

/// <summary>The Did not attend list (centre plan decision 132), run in memory as the repository runs it in SQL.</summary>
[TestFixture]
public sealed class DidNotAttendViewTests
{
    private readonly Guid _patientId = Guid.NewGuid();
    private readonly Guid _pathwayId = Guid.NewGuid();
    private readonly Guid _stageId = Guid.NewGuid();
    private List<Appointment> _all = null!;

    [SetUp]
    public void SetUp() => _all = [];

    private Appointment Add(AppointmentStatus status, DateTime? startUtc, Guid? pathwayId = null, Guid? stageId = null, DateTime? createdAtUtc = null)
    {
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = _patientId,
            PathwayId = pathwayId,
            StageId = stageId,
            Status = status,
            StartUtc = startUtc,
            EndUtc = startUtc?.AddMinutes(30),
            CreatedAtUtc = createdAtUtc ?? Now.AddDays(-30),
        };
        _all.Add(appointment);
        return appointment;
    }

    private List<Appointment> View() =>
        _all.AsQueryable()
            .Filter(AppointmentsTestConstants.CreateSearchQuery() with { View = "didNotAttend" }, _all.AsQueryable())
            .ToList();

    [Test]
    public void Filter_MissedWithNothingLater_IsListed()
    {
        var missed = Add(AppointmentStatus.DidNotAttend, Now.AddDays(-2), _pathwayId, _stageId);
        Add(AppointmentStatus.Attended, Now.AddDays(-9), _pathwayId, _stageId);

        View().Should().Equal(missed);
    }

    [Test]
    public void Filter_LaterAppointmentForTheStage_TakesItOff()
    {
        Add(AppointmentStatus.DidNotAttend, Now.AddDays(-2), _pathwayId, _stageId);
        Add(AppointmentStatus.Booked, Now.AddDays(5), _pathwayId, _stageId);

        View().Should().BeEmpty();
    }

    [Test]
    public void Filter_StillToBeBookedAfterIt_TakesItOff()
    {
        Add(AppointmentStatus.DidNotAttend, Now.AddDays(-2), _pathwayId, _stageId);
        Add(AppointmentStatus.Requested, null, _pathwayId, _stageId, createdAtUtc: Now.AddDays(-1));

        View().Should().BeEmpty();
    }

    [Test]
    public void Filter_LaterOneCancelledOrForAnotherStage_StaysListed()
    {
        var missed = Add(AppointmentStatus.DidNotAttend, Now.AddDays(-2), _pathwayId, _stageId);
        Add(AppointmentStatus.Cancelled, Now.AddDays(5), _pathwayId, _stageId);
        Add(AppointmentStatus.Booked, Now.AddDays(5), _pathwayId, Guid.NewGuid());

        View().Should().Equal(missed);
    }

    [Test]
    public void Filter_NoStage_LaterOneOnThePathwayTakesItOff()
    {
        Add(AppointmentStatus.DidNotAttend, Now.AddDays(-2), _pathwayId);
        Add(AppointmentStatus.Booked, Now.AddDays(5), _pathwayId, _stageId);

        View().Should().BeEmpty();
    }

    [Test]
    public void Filter_NoPathway_OnlyAnotherWithoutAPathwayTakesItOff()
    {
        var missed = Add(AppointmentStatus.DidNotAttend, Now.AddDays(-2));
        Add(AppointmentStatus.Booked, Now.AddDays(5), _pathwayId);

        View().Should().Equal(missed);

        Add(AppointmentStatus.Booked, Now.AddDays(6));
        View().Should().BeEmpty();
    }

    [Test]
    public void Filter_NoFurtherAppointmentDecided_TakesItOff_OtherDecisionsStay()
    {
        var rebook = Add(AppointmentStatus.DidNotAttend, Now.AddDays(-3), _pathwayId, _stageId);
        rebook.DnaFollowUp = DnaFollowUpDecision.Rebook;
        var review = Add(AppointmentStatus.DidNotAttend, Now.AddDays(-2), Guid.NewGuid());
        review.DnaFollowUp = DnaFollowUpDecision.ClinicianToReview;
        var closed = Add(AppointmentStatus.DidNotAttend, Now.AddDays(-1), Guid.NewGuid());
        closed.DnaFollowUp = DnaFollowUpDecision.NoFurtherAppointment;

        View().Should().BeEquivalentTo([rebook, review]);
    }

    [Test]
    public void Filter_OtherStatuses_AreNeverListed()
    {
        Add(AppointmentStatus.Attended, Now.AddDays(-2), Guid.NewGuid());
        Add(AppointmentStatus.Booked, Now.AddDays(2), Guid.NewGuid());

        View().Should().BeEmpty();
    }
}
