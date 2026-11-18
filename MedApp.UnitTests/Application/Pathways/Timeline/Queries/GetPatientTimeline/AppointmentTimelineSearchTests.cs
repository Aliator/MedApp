using FluentAssertions;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using EventAction = MedApp.Domain.Appointments.AppointmentEventAction;

namespace MedApp.UnitTests.Application.Pathways.Timeline.Queries.GetPatientTimeline;

[TestFixture]
public sealed class AppointmentTimelineSearchTests
{
    private static readonly PatientPathway Pathway = CreateTimelinePathway();

    private static readonly AppointmentEvent Requested = CreateAppointmentEvent(EventAction.Requested, Now.AddDays(-3));
    private static readonly AppointmentEvent Booked = CreateAppointmentEvent(EventAction.Booked, Now.AddDays(-2));
    private static readonly AppointmentEvent NotForAPathway = CreateAppointmentEvent(EventAction.Booked, Now.AddDays(-1), withPathway: false);
    private static readonly AppointmentEvent OtherPatients = CreateAppointmentEvent(EventAction.Booked, Now, patientId: OtherPatientId);
    private static readonly AppointmentEvent DeletedPathways =
        CreateAppointmentEvent(EventAction.Cancelled, Now.AddHours(-1), pathwayId: DeletedPathwayId, pathwayTitle: DeletedTitle);

    private static List<AppointmentTimelineRow> Run(GetPatientTimelineQuery query, params AppointmentEvent[] events) =>
        AppointmentTimelineSearch.Page(events.AsQueryable(), new[] { Pathway }.AsQueryable(), query).ToList();

    private static AppointmentEvent[] All() => [Requested, Booked, NotForAPathway, OtherPatients, DeletedPathways];

    [Test]
    public void Page_Staff_ReturnsThePatientsEntriesNotForAPathwayOrForOneThatExistsNewestFirst()
    {
        var rows = Run(CreateTimelineQuery(), All());

        rows.Select(r => r.Event).Should().Equal(NotForAPathway, Booked, Requested);
    }

    [Test]
    public void Page_Admin_AlsoReturnsDeletedPathwaysEntries()
    {
        var rows = Run(CreateTimelineQuery(isAdmin: true), All());

        rows.Select(r => r.Event).Should().Equal(DeletedPathways, NotForAPathway, Booked, Requested);
    }

    [Test]
    public void Page_Rows_SayWhetherThePathwayExistsAndItsTitleNow()
    {
        var rows = Run(CreateTimelineQuery(isAdmin: true), All());

        rows.Single(r => r.Event == Booked).Should().Be(new AppointmentTimelineRow(Booked, true, CurrentTitle));
        rows.Single(r => r.Event == DeletedPathways).Should().Be(new AppointmentTimelineRow(DeletedPathways, false, null));
        rows.Single(r => r.Event == NotForAPathway).Should().Be(new AppointmentTimelineRow(NotForAPathway, false, null));
    }

    [Test]
    public void Page_Rows_AreVisibleToStaffUnlessTheirPathwayIsDeleted()
    {
        var rows = Run(CreateTimelineQuery(isAdmin: true), All());

        rows.Where(r => !r.IsVisibleToStaff).Select(r => r.Event).Should().Equal(DeletedPathways);
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
    public void Page_PathwayIds_ReturnsOnlyThosePathwaysLeavingOutAppointmentsNotForOne()
    {
        var query = CreateTimelineQuery(isAdmin: true) with { PathwayIds = [DeletedPathwayId, DeletedPathwayId] };

        Run(query, All()).Select(r => r.Event).Should().Equal(DeletedPathways);
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

        Run(query, All()).Select(r => r.Event).First().Should().Be(NotForAPathway);
    }
}
