using MedApp.Application.Calendar.Common;
using MedApp.Domain.Notifications;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The clinic's meetings, past and upcoming, each arranged by an admin at a moment in the story and replayed through
/// the app's meeting steps (<see cref="MeetingStory"/>):
/// <list type="bullet">
/// <item>every week: the <b>Monday huddle</b> (08:30, everyone in) and the <b>MDT</b> (Friday 13:30, the clinicians),
/// arranged eight weeks at a time;</item>
/// <item>every month: <b>Clinical governance</b> (the second Wednesday, 16:00) and four <b>1:1 catch-ups</b>;</item>
/// <item>every six weeks: <b>mandatory training</b> for the nurses, the healthcare assistant and the physiotherapist;</item>
/// <item>one-offs: the new starter's <b>welcome</b>, <b>theatre list planning</b> on the diary day (moved, with the
/// booking coordinator added), and an <b>away day planning</b> meeting that was cancelled.</item>
/// </list>
/// Each is checked as the handlers check it: inside the working day, not on a past day, and clear of everything on
/// every attendee's calendar (<see cref="CalendarClashes"/>); a time to find is found as the app's suggested times find
/// one (<see cref="Availability"/>). The admin invites those who can go: someone busy then (on leave, part-time, in
/// another meeting) isn't invited, and someone who booked leave after it was arranged is taken off it first. People who
/// joined after it was arranged are added. Each step sends the notifications the app would.
/// </summary>
public sealed class DemoMeetings
{
    public const string Huddle = "Monday huddle";
    public const string Mdt = "MDT";
    public const string Governance = "Clinical governance";
    public const string OneToOne = "1:1 catch-up";
    public const string Welcome = "Welcome and induction";
    public const string TheatrePlanning = "Theatre list planning";
    public const string AwayDayPlanning = "Away day planning";

    private readonly DemoCalendars _calendars;
    private readonly IReadOnlyList<StaffMember> _staff;
    private readonly DemoTimeline _timeline;
    private readonly List<MeetingStory> _stories = [];

    private DemoMeetings(DemoCalendars calendars, IReadOnlyList<StaffMember> staff, DemoTimeline timeline)
    {
        _calendars = calendars;
        _staff = staff;
        _timeline = timeline;
    }

    /// <summary>Every meeting's history, cancelled ones included.</summary>
    public IReadOnlyList<MeetingStory> Stories => _stories;

    /// <summary>The meetings moved (once arranged), and the one cancelled, for the summary.</summary>
    public MeetingStory? Moved { get; private set; }
    public MeetingStory? MovedOneToOne { get; private set; }
    public MeetingStory? Replanned { get; private set; }
    public MeetingStory? Cancelled { get; private set; }

    /// <summary>
    /// The notifications the meetings' histories sent in the last 30 days (<see cref="DemoTimeline.NotificationsFromUtc"/>),
    /// as for the rest of the story: older ones would long have been read.
    /// </summary>
    public IEnumerable<Notification> RecentNotifications =>
        _stories.SelectMany(s => s.Notifications)
            .Where(n => n.CreatedAtUtc >= _timeline.NotificationsFromUtc && n.CreatedAtUtc <= _timeline.NowUtc);

    /// <summary>Plans every meeting, adding each one that stands to the calendars as it's planned, so later ones avoid it.</summary>
    public static DemoMeetings Plan(DemoCalendars calendars, IReadOnlyList<StaffMember> staff, DemoTimeline timeline)
    {
        var meetings = new DemoMeetings(calendars, staff, timeline);
        meetings.Build();
        return meetings;
    }

    private StaffMember Member(string username) => DemoStaff.Get(_staff, username);

    private StaffMember Lead => _staff.Single(s => s.Kind == StaffKind.ClinicalLead);

    private StaffMember Manager => Member(DemoStaff.ManagerUsername);

    /// <summary>The service manager arranges things once they're an admin; the lead before that.</summary>
    private StaffMember ManagerOrLeadAt(DateTime utc) => Manager.IsAdminAt(utc) ? Manager : Lead;

    private IEnumerable<StaffMember> OfKind(params StaffKind[] kinds) => _staff.Where(s => kinds.Contains(s.Kind));

    private void Build()
    {
        var firstMonday = _timeline.ThisMonday.AddDays(-7 * VolumeCurves.HistoryWeeks);
        var horizon = _timeline.Today.AddDays(7 * (MeetingSchedule.BatchWeeks + MeetingSchedule.BatchLeadWeeks));
        var now = _timeline.NowUtc;

        // Weekly: everyone in on Monday morning; the clinicians' MDT on Friday afternoon.
        var everyone = _staff.Where(s => s.Kind != StaffKind.ClinicalLead).Prepend(Lead).ToList();
        foreach (var date in MeetingSchedule.Weekly(firstMonday, horizon, DayOfWeek.Monday))
            if (BatchArrangedUtc(date, firstMonday) is { } arranged)
                Arrange(Huddle, "The week ahead: cover, leave and room changes.", date, new(8, 30), 15, everyone, _ => Lead, arranged);

        var clinicians = OfKind(StaffKind.ClinicalLead, StaffKind.Surgeon, StaffKind.Leaver, StaffKind.Endoscopist, StaffKind.Nurse, StaffKind.MinorProceduresNurse).ToList();
        foreach (var date in MeetingSchedule.Weekly(firstMonday, horizon, DayOfWeek.Friday))
            if (BatchArrangedUtc(date, firstMonday) is { } arranged)
                Arrange(Mdt, "Weekly multidisciplinary team meeting. The agenda goes round on Thursday.", date, new(13, 30), 60, clinicians, _ => Lead, arranged);

        // Monthly: clinical governance, arranged three weeks ahead.
        var governance = OfKind(StaffKind.ClinicalLead, StaffKind.ServiceManager, StaffKind.Surgeon, StaffKind.Leaver, StaffKind.Endoscopist,
            StaffKind.Nurse, StaffKind.MinorProceduresNurse, StaffKind.PartTimeNurse, StaffKind.Physiotherapist).ToList();
        foreach (var date in MeetingSchedule.MonthlyNth(firstMonday, horizon, DayOfWeek.Wednesday, 2))
            if (ArrangedUtc(date.AddDays(-21), 14, 0) is { } arranged)
                Arrange(Governance, "Incidents and learning, audit results and policy updates.", date, new(16, 0), 60, governance, ManagerOrLeadAt, arranged);

        // Every six weeks: mandatory training, at a time everyone going is free (Tuesday to Thursday, from 09:00).
        var trainees = OfKind(StaffKind.Nurse, StaffKind.MinorProceduresNurse, StaffKind.PartTimeNurse, StaffKind.HealthcareAssistant,
            StaffKind.NewStarter, StaffKind.Physiotherapist).ToList();
        var session = 0;
        for (var monday = firstMonday.AddDays(14); monday <= horizon; monday = monday.AddDays(7 * MeetingSchedule.TrainingEveryWeeks), session++)
        {
            if (ArrangedUtc(monday.AddDays(-28), 10, 0) is not { } arranged)
                continue;
            var days = new[] { monday.AddDays(1), monday.AddDays(2), monday.AddDays(3) };
            var going = trainees.Where(t => t.StartedUtc <= arranged && t.WorksAt(UkTime.ToUtc(days[^1], WorkingHours.End))
                                            && !days.Any(d => _calendars.IsOnLeave(t.Id, d))).ToList();
            if (FindTime(going, days, 90, new(9, 0), arranged) is { } time)
                Arrange(MeetingSchedule.TrainingTitle(session), "Mandatory training in the seminar room. Bring your training record.",
                    time, going, ManagerOrLeadAt, arranged);
        }

        // Monthly 1:1s, over the last four months and the next: the service manager with the office and the healthcare
        // assistant, the lead with two nurses. In the week of the month's second Tuesday, from 10:00.
        var pairs = new[]
        {
            (Manager, Member(DemoStaff.CoordinatorUsername)),
            (Manager, Member(DemoStaff.LockedUsername)),
            (Lead, Member("k.ellison")),
            (Lead, Member("s.brightwell")),
        };
        var firstMonth = new DateOnly(_timeline.Today.Year, _timeline.Today.Month, 1).AddMonths(-4);
        foreach (var tuesday in MeetingSchedule.MonthlyNth(firstMonth, firstMonth.AddMonths(6).AddDays(-1), DayOfWeek.Tuesday, 2))
        foreach (var (manager, report) in pairs)
        {
            if (ArrangedUtc(tuesday.AddDays(-14), 10, 0) is not { } arranged)
                continue;
            var days = new[] { tuesday, tuesday.AddDays(1), tuesday.AddDays(2) };
            if (FindTime([manager, report], days, 30, new(10, 0), arranged) is { } time)
                Arrange(OneToOne, "Monthly catch-up: workload, leave and training.", time, [manager, report],
                    t => manager == Manager ? ManagerOrLeadAt(t) : Lead, arranged);
        }

        WelcomeTheNewStarter();
        PlanTheatreLists(now);
        CancelTheAwayDay(now);
        MoveTheNextGovernance(now);
        MoveTheNextOneToOne(now);
    }

    /// <summary>The new starter's first morning (or the next free one): with the nurse who'll mentor them, and the service manager.</summary>
    private void WelcomeTheNewStarter()
    {
        var starter = _staff.Single(s => s.Kind == StaffKind.NewStarter);
        var people = new[] { starter, Member("k.ellison"), Manager };
        var arranged = starter.StartedUtc.AddMinutes(20);
        var days = WorkingDaysFrom(UkTime.DateOf(starter.StartedUtc).AddDays(1), 3);
        if (FindTime(people, days, 60, new(9, 30), arranged) is { } time)
            Arrange(Welcome, "Introductions, a tour of the unit, IT accounts and the mandatory training plan.", time, people, ManagerOrLeadAt, arranged);
    }

    /// <summary>
    /// Theatre list planning on the lead's diary day, arranged nine days ago with the surgeons (in the afternoon if they
    /// can); three days ago it moved and the booking coordinator was added, so its notifications ("Meeting moved",
    /// "Meeting invitation") show.
    /// </summary>
    private void PlanTheatreLists(DateTime now)
    {
        const int minutes = 30;
        var arranged = DemoTimeline.WorkingMoment(now.AddDays(-9), 10, 0);
        var people = new[] { Lead, Member("r.calloway"), Member("d.ashworth") };
        var day = _timeline.DiaryDay;
        if ((FindTime(people, [day], minutes, new(12, 0), arranged) ?? FindTime(people, [day], minutes, WorkingHours.Start, arranged)) is not { } time
            || Arrange(TheatrePlanning, "Next month's theatre lists, and cover for leave.", time, people, ManagerOrLeadAt, arranged) is not { } story)
            return;

        // To the first time after it (else before it) when the coordinator can come too, else when the surgeons can.
        var changed = DemoTimeline.WorkingMoment(now.AddDays(-3), 15, 0);
        var coordinator = Member(DemoStaff.CoordinatorUsername);
        var current = story.Meeting.StartUtc;
        var starts = Enumerable.Range(0, (int)(WorkingHours.End - WorkingHours.Start).TotalMinutes / ScheduleAllocator.StepMinutes)
            .Select(i => UkTime.ToUtc(day, WorkingHours.Start.AddMinutes(ScheduleAllocator.StepMinutes * i)))
            .Where(s => s != current)
            .OrderBy(s => s < current)
            .ThenBy(s => s < current ? -s.Ticks : s.Ticks)
            .ToList();
        foreach (var going in new[] { people.Append(coordinator).ToArray(), people })
        {
            if (starts.FirstOrDefault(s => IsFreeForAll(story, going, s, s.AddMinutes(minutes))) is not { Ticks: > 0 } start)
                continue;
            story.Change(going.Select(p => p.Id).ToList(), start, start.AddMinutes(minutes), ManagerOrLeadAt(changed), changed);
            Replanned = story;
            return;
        }
    }

    /// <summary>Away day planning, arranged for next week, then cancelled four days ago: only its notifications remain.</summary>
    private void CancelTheAwayDay(DateTime now)
    {
        var arranged = DemoTimeline.WorkingMoment(now.AddDays(-18), 11, 0);
        var people = new[] { Lead, Manager, Member("r.calloway"), Member("d.ashworth"), Member(DemoStaff.NoAssignmentNotificationsUsername), Member("k.ellison") };
        var nextMonday = _timeline.ThisMonday.AddDays(7);
        if (FindTime(people, [nextMonday.AddDays(1), nextMonday.AddDays(2), nextMonday.AddDays(3)], 60, new(14, 0), arranged) is not { } time
            || Arrange(AwayDayPlanning, "Agenda and venue for the spring away day.", time, people, ManagerOrLeadAt, arranged) is not { } story)
            return;

        var cancelled = DemoTimeline.WorkingMoment(now.AddDays(-4), 15, 30);
        story.Cancel(ManagerOrLeadAt(cancelled), cancelled);
        _calendars.RemoveMeeting(story.Meeting);
        Cancelled = story;
    }

    /// <summary>The next governance meeting, moved to the next day (or an hour earlier) by whoever arranges them.</summary>
    private void MoveTheNextGovernance(DateTime now)
    {
        if (NextUpcoming(Governance, now) is not { } story)
            return;
        var start = story.Meeting.StartUtc;
        var end = story.Meeting.EndUtc;
        Moved = TryMove(story, [(start.AddDays(1), end.AddDays(1)), (start.AddHours(-1), end.AddHours(-1)), (start.AddDays(1).AddHours(-1), end.AddDays(1).AddHours(-1))],
            ManagerOrLeadAt, now.AddDays(-10), 11, 0);
    }

    /// <summary>The lead's next 1:1 with the pre-assessment nurse, moved an hour or so.</summary>
    private void MoveTheNextOneToOne(DateTime now)
    {
        var nurse = Member("k.ellison");
        if (NextUpcoming(OneToOne, now, s => s.Meeting.Attendees.Any(a => a.StaffUserId == nurse.Id)) is not { } story)
            return;
        var start = story.Meeting.StartUtc;
        var end = story.Meeting.EndUtc;
        MovedOneToOne = TryMove(story, new[] { 60, 90, -60, 120 }.Select(m => (start.AddMinutes(m), end.AddMinutes(m))).ToList(),
            _ => Lead, now.AddDays(-5), 9, 15);
    }

    private MeetingStory? NextUpcoming(string title, DateTime now, Func<MeetingStory, bool>? also = null) =>
        _stories.Where(s => !s.IsCancelled && s.Meeting.Title == title && s.Meeting.StartUtc > now && (also?.Invoke(s) ?? true))
            .OrderBy(s => s.Meeting.StartUtc).FirstOrDefault();

    /// <summary>
    /// Moves a meeting to the first of some times when everyone going is free, at a working moment from
    /// <paramref name="notBefore"/> (and after it was last changed), if that's still before now and its day. Null if it
    /// can't move.
    /// </summary>
    private MeetingStory? TryMove(
        MeetingStory story, IReadOnlyList<(DateTime Start, DateTime End)> times, Func<DateTime, StaffMember> by, DateTime notBefore, int hour, int minute)
    {
        var after = story.Meeting.LastUpdated.AddDays(1);
        var at = DemoTimeline.WorkingMoment(notBefore > after ? notBefore : after, hour, minute);
        if (at > _timeline.NowUtc || UkTime.DateOf(at) > UkTime.DateOf(story.Meeting.StartUtc))
            return null;

        var people = _staff.Where(s => story.Meeting.Attendees.Any(a => a.StaffUserId == s.Id)).ToList();
        foreach (var (start, end) in times)
        {
            if (UkTime.DateOf(start) < UkTime.DateOf(at) || !IsFreeForAll(story, people, start, end))
                continue;
            story.Change(people.Select(p => p.Id).ToList(), start, end, by(at), at);
            return story;
        }

        return null;
    }

    private bool IsFreeForAll(MeetingStory story, IEnumerable<StaffMember> people, DateTime start, DateTime end)
    {
        if (WorkingHours.WhyOutside(start, end) is not null)
            return false;
        var date = UkTime.DateOf(start);
        return people.All(p => CalendarClashes.FindFirst(_calendars.DataFor(p.Id, date, date), start, end, ignoreMeetingId: story.Meeting.Id) is null);
    }

    /// <summary>When a recurring meeting's batch was arranged (09:30 on its Monday), or null if that's not happened yet.</summary>
    private DateTime? BatchArrangedUtc(DateOnly date, DateOnly firstMonday) =>
        ArrangedUtc(MeetingSchedule.BatchArrangedOn(date, firstMonday), 9, 30);

    /// <summary>
    /// A working moment on or after a date when something was arranged: not before the clinic got going (the Friday
    /// before it started using MedApp), and null if it's still to come.
    /// </summary>
    private DateTime? ArrangedUtc(DateOnly date, int hour, int minute)
    {
        var earliest = DemoTimeline.WorkingMoment(_timeline.AppStartUtc.AddDays(-3), 9, 30);
        var at = DemoTimeline.WorkingMoment(DemoTimeline.At(date, hour, minute), hour, minute);
        if (at < earliest)
            at = earliest;
        return at <= _timeline.NowUtc.AddMinutes(-30) ? at : null;
    }

    private static List<DateOnly> WorkingDaysFrom(DateOnly first, int count)
    {
        var days = new List<DateOnly>();
        for (var date = first; days.Count < count; date = date.AddDays(1))
            if (WorkingHours.IsWorkingDay(date))
                days.Add(date);
        return days;
    }

    /// <summary>When all of them are free, as the app's suggested times find it, from a clock time.</summary>
    private TimeRange? FindTime(IReadOnlyCollection<StaffMember> people, IReadOnlyCollection<DateOnly> days, int minutes, TimeOnly notBefore, DateTime arranged) =>
        people.Count == 0
            ? null
            : MeetingSchedule.FirstFreeTime(
                people.Select(p => _calendars.DataFor(p.Id, days.Min(), days.Max())).ToList(), days, minutes, notBefore, arranged);

    private MeetingStory? Arrange(string title, string notes, DateOnly date, TimeOnly at, int minutes, IEnumerable<StaffMember> invitees,
        Func<DateTime, StaffMember> by, DateTime arranged)
    {
        var start = UkTime.ToUtc(date, at);
        return Arrange(title, notes, new TimeRange(start, start.AddMinutes(minutes)), invitees, by, arranged);
    }

    /// <summary>
    /// Arranges a meeting with whoever of <paramref name="invitees"/> can go, and replays what followed: people who
    /// joined the clinic later added, and people who booked leave (or other time) over it later taken off first, as
    /// the app would need before that time could be booked. Null if nobody could go.
    /// </summary>
    private MeetingStory? Arrange(string title, string notes, TimeRange time, IEnumerable<StaffMember> invitees,
        Func<DateTime, StaffMember> by, DateTime arranged)
    {
        var (start, end) = (time.StartUtc, time.EndUtc);
        var date = UkTime.DateOf(start);
        var invited = new List<StaffMember>();
        var later = new List<(DateTime At, StaffMember Member, bool Joins)>();

        foreach (var member in invitees.Distinct())
        {
            // Gone before it was arranged, or not here by the time it happens. (Someone leaving later stays invited:
            // deleting their account takes them off what hasn't happened yet, as the app does.)
            if (member.LeftUtc is { } left && left <= arranged || member.StartedUtc >= start)
                continue;

            var data = _calendars.DataFor(member.Id, date, date);
            if (CalendarClashes.FindFirst(data, start, end) is null)
            {
                if (member.StartedUtc <= arranged)
                    invited.Add(member);
                else
                    later.Add((JoinedAt(member, start), member, true));
                continue;
            }

            // Busy only with what they stored after it was arranged (leave booked later, say): they were invited,
            // then taken off it before that could be saved.
            var lateBlocks = data.Blocks.Where(b => b.CreatedAtUtc > arranged).ToList();
            if (member.StartedUtc > arranged || lateBlocks.Count == 0)
                continue;
            var lateDays = lateBlocks.Select(b => UkTime.DateOf(b.StartUtc)).ToHashSet();
            var then = data with
            {
                Blocks = data.Blocks.Except(lateBlocks).ToList(),
                DayOverrides = data.DayOverrides.Where(o => !lateDays.Contains(o.Date)).ToList(),
            };
            if (CalendarClashes.FindFirst(then, start, end) is not null)
                continue;

            invited.Add(member);
            var booked = lateBlocks.Where(b => CalendarClashes.Overlaps(start, end, b.StartUtc, b.EndUtc)).Min(b => b.CreatedAtUtc);
            var takenOff = booked.AddMinutes(-15);
            later.Add((takenOff > arranged ? takenOff : MeetingSchedule.Between(arranged, booked), member, false));
        }

        if (invited.Count == 0)
            return null;

        var story = MeetingStory.Arrange(title, notes, start, end, invited.Select(m => m.Id).ToList(), by(arranged), arranged);
        var going = invited.Select(m => m.Id).ToList();
        foreach (var (at, member, joins) in later.OrderBy(l => l.At))
        {
            if (joins)
                going.Add(member.Id);
            else
                going.Remove(member.Id);

            if (going.Count == 0)
            {
                story.Cancel(by(at), at);
                break;
            }

            story.Change(going.ToList(), start, end, by(at), at);
        }

        _stories.Add(story);
        if (!story.IsCancelled)
            _calendars.AddMeeting(story.Meeting);
        return story;
    }

    /// <summary>Added once they'd joined: a couple of hours after their account was made, before the meeting and before now.</summary>
    private DateTime JoinedAt(StaffMember member, DateTime start)
    {
        var latest = start < _timeline.NowUtc ? start : _timeline.NowUtc;
        var at = member.StartedUtc.AddHours(2);
        return at < latest ? at : MeetingSchedule.Between(member.StartedUtc, latest);
    }
}
