using FluentAssertions;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Appointments;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using EventAction = MedApp.Domain.Appointments.AppointmentEventAction;

namespace MedApp.UnitTests.Application.Procedures.Timeline.Queries.GetPatientTimeline;

[TestFixture]
public sealed class AppointmentTimelineSearchTests
{
    private static readonly PatientProcedure Procedure = CreateTimelineProcedure();

    private static readonly AppointmentEvent Requested = CreateAppointmentEvent(EventAction.Requested, Now.AddDays(-3));
    private static readonly AppointmentEvent Booked = CreateAppointmentEvent(EventAction.Booked, Now.AddDays(-2));
    private static readonly AppointmentEvent NotForAProcedure = CreateAppointmentEvent(EventAction.Booked, Now.AddDays(-1), withProcedure: false);
    private static readonly AppointmentEvent OtherPatients = CreateAppointmentEvent(EventAction.Booked, Now, patientId: OtherPatientId);
    private static readonly AppointmentEvent DeletedProcedures =
        CreateAppointmentEvent(EventAction.Cancelled, Now.AddHours(-1), procedureId: DeletedProcedureId, procedureTitle: DeletedTitle);

    private static List<AppointmentTimelineRow> Run(GetPatientTimelineQuery query, params AppointmentEvent[] events) =>
        AppointmentTimelineSearch.Page(events.AsQueryable(), new[] { Procedure }.AsQueryable(), query).ToList();

    private static AppointmentEvent[] All() => [Requested, Booked, NotForAProcedure, OtherPatients, DeletedProcedures];

    [Test]
    public void Page_Staff_ReturnsThePatientsEntriesNotForAProcedureOrForOneThatExistsNewestFirst()
    {
        var rows = Run(CreateTimelineQuery(), All());

        rows.Select(r => r.Event).Should().Equal(NotForAProcedure, Booked, Requested);
    }

    [Test]
    public void Page_Admin_AlsoReturnsDeletedProceduresEntries()
    {
        var rows = Run(CreateTimelineQuery(isAdmin: true), All());

        rows.Select(r => r.Event).Should().Equal(DeletedProcedures, NotForAProcedure, Booked, Requested);
    }

    [Test]
    public void Page_Rows_SayWhetherTheProcedureExistsAndItsTitleNow()
    {
        var rows = Run(CreateTimelineQuery(isAdmin: true), All());

        rows.Single(r => r.Event == Booked).Should().Be(new AppointmentTimelineRow(Booked, true, CurrentTitle));
        rows.Single(r => r.Event == DeletedProcedures).Should().Be(new AppointmentTimelineRow(DeletedProcedures, false, null));
        rows.Single(r => r.Event == NotForAProcedure).Should().Be(new AppointmentTimelineRow(NotForAProcedure, false, null));
    }

    [Test]
    public void Page_Rows_AreVisibleToStaffUnlessTheirProcedureIsDeleted()
    {
        var rows = Run(CreateTimelineQuery(isAdmin: true), All());

        rows.Where(r => !r.IsVisibleToStaff).Select(r => r.Event).Should().Equal(DeletedProcedures);
    }

    [TestCase("Appointments")]
    [TestCase("Appointments", "Notes")]
    [TestCase("appointments")]
    public void Page_KindsIncludingAppointments_ReturnsEveryEntry(params string[] kinds)
    {
        var query = CreateTimelineQuery() with { Kinds = kinds };

        Run(query, All()).Should().HaveCount(3);
    }

    [TestCase("Notes")]
    [TestCase("Stages", "Status")]
    public void Page_KindsWithoutAppointments_ReturnsNothing(params string[] kinds)
    {
        var query = CreateTimelineQuery(isAdmin: true) with { Kinds = kinds };

        Run(query, All()).Should().BeEmpty();
    }

    [Test]
    public void Page_ProcedureIds_ReturnsOnlyThoseProceduresLeavingOutAppointmentsNotForOne()
    {
        var query = CreateTimelineQuery(isAdmin: true) with { ProcedureIds = [DeletedProcedureId, DeletedProcedureId] };

        Run(query, All()).Select(r => r.Event).Should().Equal(DeletedProcedures);
    }

    [Test]
    public void Page_TakesOneMoreThanThePageSize()
    {
        Run(CreateTimelineQuery(pageSize: 1), All()).Should().HaveCount(2);
    }

    [Test]
    public void Page_EntriesAtTheSameMoment_AreOrderedByIdDescending()
    {
        var first = CreateAppointmentEvent(EventAction.Booked, Now, id: Guid.Parse("00000022-0000-0000-0000-00000000000a"));
        var second = CreateAppointmentEvent(EventAction.NotesUpdated, Now, id: Guid.Parse("00000022-0000-0000-0000-00000000000b"));

        Run(CreateTimelineQuery(), first, second).Select(r => r.Event).Should().Equal(second, first);
    }

    [Test]
    public void Page_FollowingEachCursor_ReturnsEveryEntryOnceEvenWhenTimesTie()
    {
        // Ten entries over four moments, so pages end in the middle of a moment.
        var events = Enumerable.Range(0, 10)
            .Select(i => CreateAppointmentEvent(EventAction.NotesUpdated, Now.AddMinutes(-(i % 4))))
            .ToArray();
        var seen = new List<AppointmentEvent>();
        string? before = null;

        for (var pages = 0; pages < 10; pages++)
        {
            var rows = Run(CreateTimelineQuery(pageSize: 3) with { Before = before }, events);
            var page = rows.Take(3).Select(r => r.Event).ToList();
            seen.AddRange(page);
            if (rows.Count <= 3)
                break;
            before = TimelineCursor.After(page[^1]).Encode();
        }

        seen.Should().OnlyHaveUniqueItems().And.HaveCount(10);
        seen.Should().BeInDescendingOrder(e => e.AtUtc);
    }

    [Test]
    public void Page_UnreadableCursor_StartsFromTheNewest()
    {
        var query = CreateTimelineQuery() with { Before = "not a cursor" };

        Run(query, All()).Select(r => r.Event).First().Should().Be(NotForAProcedure);
    }
}
