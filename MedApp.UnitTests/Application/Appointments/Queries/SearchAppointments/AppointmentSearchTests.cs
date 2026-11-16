using FluentAssertions;
using MedApp.Application.Appointments.Queries.SearchAppointments;
using MedApp.Domain.Appointments;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Queries.SearchAppointments;

[TestFixture]
public sealed class AppointmentSearchTests
{
    private Appointment _mondayTen = null!;
    private Appointment _mondayNine = null!;
    private Appointment _requestedWithProcedure = null!;
    private Appointment _requestedEarlier = null!;
    private Appointment _cancelledFriday = null!;

    [SetUp]
    public void SetUp()
    {
        _mondayTen = Make(AppointmentStatus.Booked, At(NextMonday, 10), "Ada", "Zephyr", createdDaysAgo: 3, dueInDays: 10);
        _mondayNine = Make(AppointmentStatus.Booked, At(NextMonday, 9), "Ben", "Young", createdDaysAgo: 1);
        _mondayNine.StaffUserId = OtherStaffUserId;
        _requestedWithProcedure = Make(AppointmentStatus.Requested, null, "Cara", "Xavier", createdDaysAgo: 2, dueInDays: 5);
        _requestedEarlier = Make(AppointmentStatus.Requested, null, "Dan", "Walsh", createdDaysAgo: 4);
        _cancelledFriday = Make(AppointmentStatus.Cancelled, At(NextFriday, 10), "Eve", "young", createdDaysAgo: 5, dueInDays: 1);
    }

    private static Appointment Make(
        AppointmentStatus status, DateTime? startUtc, string firstName, string lastName, int createdDaysAgo, int? dueInDays = null)
    {
        var patient = PatientsTestConstants.CreatePatient(firstName, lastName, new DateOnly(1980, 1, 1), $"{firstName}@example.test");
        var booked = status != AppointmentStatus.Requested;
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Patient = patient,
            Status = status,
            StaffUserId = booked ? StaffUserId : null,
            StartUtc = startUtc,
            EndUtc = startUtc?.AddMinutes(30),
            CreatedAtUtc = Now.AddDays(-createdDaysAgo),
            CreatedByUserId = StaffUserId,
        };

        if (dueInDays is { } days)
        {
            var procedure = ProceduresTestConstants.CreateProcedure(patient: patient, dueDateUtc: Now.AddDays(days));
            appointment.ProcedureId = procedure.Id;
            appointment.Procedure = procedure;
            appointment.StageId = procedure.StageAt(1).Id;
            appointment.Stage = procedure.StageAt(1);
        }

        return appointment;
    }

    private IQueryable<Appointment> All() =>
        new[] { _cancelledFriday, _requestedEarlier, _mondayTen, _requestedWithProcedure, _mondayNine }.AsQueryable();

    private static SearchAppointmentsQuery Query() => AppointmentsTestConstants.CreateSearchQuery();

    [Test]
    public void Filter_NoFilters_ReturnsEveryAppointment()
    {
        All().Filter(Query()).Should().HaveCount(5);
    }

    [Test]
    public void Filter_Patient_MatchesTheirs()
    {
        All().Filter(Query() with { PatientId = _mondayNine.PatientId }).Should().Equal(_mondayNine);
    }

    [Test]
    public void Filter_ProcedureAndStage_MatchThem()
    {
        All().Filter(Query() with { ProcedureId = _mondayTen.ProcedureId }).Should().Equal(_mondayTen);
        All().Filter(Query() with { StageId = _requestedWithProcedure.StageId }).Should().Equal(_requestedWithProcedure);
    }

    [Test]
    public void Filter_StaffMember_MatchesTheirs()
    {
        All().Filter(Query() with { StaffUserId = OtherStaffUserId }).Should().Equal(_mondayNine);
    }

    [Test]
    public void Filter_Statuses_MatchAnyOfThemIgnoringCase()
    {
        All().Filter(Query() with { Statuses = ["requested", "Cancelled", "Cancelled"] })
            .Should().BeEquivalentTo([_cancelledFriday, _requestedEarlier, _requestedWithProcedure]);
    }

    [Test]
    public void Filter_UnknownOrEmptyStatuses_AreIgnored()
    {
        All().Filter(Query() with { Statuses = ["Pending"] }).Should().HaveCount(5);
        All().Filter(Query() with { Statuses = [] }).Should().HaveCount(5);
    }

    [Test]
    public void Filter_DateRange_IncludesBothDaysAndLeavesOutRequested()
    {
        All().Filter(Query() with { From = NextMonday, To = NextMonday }).Should().BeEquivalentTo([_mondayTen, _mondayNine]);
        All().Filter(Query() with { From = NextMonday, To = NextFriday }).Should().HaveCount(3);
    }

    [Test]
    public void Filter_FromOnly_IsOnOrAfterThatDay()
    {
        All().Filter(Query() with { From = NextFriday }).Should().Equal(_cancelledFriday);
    }

    [Test]
    public void Filter_ToOnly_IsOnOrBeforeThatDay()
    {
        All().Filter(Query() with { To = NextMonday }).Should().BeEquivalentTo([_mondayTen, _mondayNine]);
    }

    [Test]
    public void Filter_Dates_AreUkDays()
    {
        // 23:30 UTC on Sunday 14 June is 00:30 BST on Monday 15 June.
        var lateSunday = Make(AppointmentStatus.Booked, new DateTime(2026, 6, 14, 23, 30, 0, DateTimeKind.Utc), "Fay", "Vale", 1);
        var appointments = new[] { lateSunday }.AsQueryable();

        appointments.Filter(Query() with { From = SummerMonday, To = SummerMonday }).Should().Equal(lateSunday);
        appointments.Filter(Query() with { To = SummerMonday.AddDays(-1) }).Should().BeEmpty();
    }

    [Test]
    public void Filter_Search_MatchesPartOfTheFullNameIgnoringCaseAndSpaces()
    {
        All().Filter(Query() with { Search = "  BEN YOU " }).Should().Equal(_mondayNine);
        All().Filter(Query() with { Search = "young" }).Should().BeEquivalentTo([_mondayNine, _cancelledFriday]);
    }

    [Test]
    public void Filter_BlankSearch_IsIgnored()
    {
        All().Filter(Query() with { Search = "   " }).Should().HaveCount(5);
    }

    [Test]
    public void Sort_Default_ByStartWithRequestedLastByWhenAskedFor()
    {
        All().Sort(null, true).Should().Equal(_mondayNine, _mondayTen, _cancelledFriday, _requestedEarlier, _requestedWithProcedure);
    }

    [Test]
    public void Sort_StartDescending_StillPutsRequestedLast()
    {
        All().Sort("START", false).Should().Equal(_cancelledFriday, _mondayTen, _mondayNine, _requestedEarlier, _requestedWithProcedure);
    }

    [Test]
    public void Sort_UnknownColumn_SortsByStart()
    {
        All().Sort("notes", true).Should().Equal(All().Sort(AppointmentSearch.SortByStart, true));
    }

    [Test]
    public void Sort_Requested_ByWhenAskedFor()
    {
        All().Sort(AppointmentSearch.SortByRequested, true)
            .Should().Equal(_cancelledFriday, _requestedEarlier, _mondayTen, _requestedWithProcedure, _mondayNine);
        All().Sort(AppointmentSearch.SortByRequested, false)
            .Should().Equal(_mondayNine, _requestedWithProcedure, _mondayTen, _requestedEarlier, _cancelledFriday);
    }

    [Test]
    public void Sort_DueDate_ByTheProceduresWithNoneLast()
    {
        All().Sort(AppointmentSearch.SortByDueDate, true)
            .Should().Equal(_cancelledFriday, _requestedWithProcedure, _mondayTen, _requestedEarlier, _mondayNine);
        All().Sort(AppointmentSearch.SortByDueDate, false)
            .Should().Equal(_mondayTen, _requestedWithProcedure, _cancelledFriday, _requestedEarlier, _mondayNine);
    }

    [Test]
    public void Sort_Patient_ByLastThenFirstNameIgnoringCase()
    {
        All().Sort(AppointmentSearch.SortByPatient, true)
            .Should().Equal(_requestedEarlier, _requestedWithProcedure, _mondayNine, _cancelledFriday, _mondayTen);
        All().Sort(AppointmentSearch.SortByPatient, false)
            .Should().Equal(_mondayTen, _cancelledFriday, _mondayNine, _requestedWithProcedure, _requestedEarlier);
    }

    [Test]
    public void Sort_Ties_AreBrokenByWhenAskedForThenId()
    {
        var first = Make(AppointmentStatus.Requested, null, "Gus", "Same", 1);
        var second = Make(AppointmentStatus.Requested, null, "Gus", "Same", 1);
        var expected = new[] { first, second }.OrderBy(a => a.Id).ToArray();

        new[] { expected[1], expected[0] }.AsQueryable().Sort(AppointmentSearch.SortByPatient, true).Should().Equal(expected);
        new[] { expected[1], expected[0] }.AsQueryable().Sort(null, false).Should().Equal(expected);
    }

    [TestCase("start", true)]
    [TestCase("DueDate", true)]
    [TestCase("PATIENT", true)]
    [TestCase("requested", true)]
    [TestCase("notes", false)]
    [TestCase("", false)]
    public void IsSortColumn_IgnoresCase(string sort, bool expected)
    {
        AppointmentSearch.IsSortColumn(sort).Should().Be(expected);
    }

    [TestCase("Booked", true)]
    [TestCase("didnotattend", true)]
    [TestCase("4", false)]
    [TestCase(null, false)]
    [TestCase("Pending", false)]
    public void IsStatus_ByNameOnly(string? value, bool expected)
    {
        AppointmentSearch.IsStatus(value).Should().Be(expected);
    }

    [Test]
    public void StatusNames_ListsEveryStatus()
    {
        AppointmentSearch.StatusNames.Should().Be("Requested, Booked, Attended, DidNotAttend, Cancelled");
    }
}
