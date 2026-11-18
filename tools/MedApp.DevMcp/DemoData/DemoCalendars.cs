using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;
using MedApp.Application.Calendar.WeekTemplates.Commands.CreateWeekTemplate;
using MedApp.Domain.Calendar;
using MedApp.Infrastructure.Data;
using MedApp.Infrastructure.Data.Configurations.Calendar;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Everyone's calendars: the activity types the clinic added to the seeded ones, each person's own day and week
/// templates (<see cref="DemoTemplates"/>: templates are personal) and default week (dated from before the clinic
/// started, so past weeks show their blocks, with a part-time change and a moved lunch partway through), the
/// exceptions (leave, a training day, a moved lunch, a removed block, a day using another template) and the clinic's
/// meetings (<see cref="DemoMeetings"/>). The schedule allocator is built from the same rows through the app's
/// <see cref="CalendarProjection"/>, so appointments never land on a block or a meeting.
/// <para>
/// Every stored block and meeting obeys the app's no-overlap rule (<see cref="CalendarClashes"/>), checked as the app
/// checks a new one: a day of leave or training first sets that day to no day template (a day override with none, as
/// "This day's template: none" saves), as the app needs before a whole-day block can go on it; a moved lunch goes
/// only into free time; a meeting goes only to those free then.
/// </para>
/// </summary>
public sealed class DemoCalendars
{
    private static readonly CreateDayTemplateValidator DayValidator = new();
    private static readonly CreateWeekTemplateValidator WeekValidator = new();

    private readonly Dictionary<string, CalendarActivityType> _types = [];
    private readonly Dictionary<(Guid Owner, string Name), DayTemplate> _days = [];
    private readonly Dictionary<(Guid Owner, string Name), WeekTemplate> _weeks = [];
    private readonly List<StaffWeekTemplate> _staffWeeks = [];
    private readonly List<CalendarBlock> _blocks = [];
    private readonly List<CalendarBlockException> _exceptions = [];
    private readonly List<CalendarDayOverride> _overrides = [];
    private readonly List<Meeting> _meetings = [];

    private DemoCalendars()
    {
    }

    public ScheduleAllocator Allocator { get; } = new();

    /// <summary>The removed block's day: a nurse's admin time taken off, and an appointment booked in its place.</summary>
    public (Guid StaffUserId, DateTime StartUtc, DateTime EndUtc)? FreedSlot { get; private set; }

    public int ActivityTypesAdded => _types.Values.Count(t => !CalendarActivityTypeConfiguration.Seeded.Any(s => s.Id == t.Id));
    public int DayTemplates => _days.Count;
    public int WeekTemplates => _weeks.Count;
    public int Blocks => _blocks.Count;
    public int Exceptions => _exceptions.Count;
    public int Overrides => _overrides.Count;

    /// <summary>The clinic's meetings, with their histories and notifications.</summary>
    public DemoMeetings MeetingPlan { get; private set; } = null!;

    /// <summary>The meetings as saved (cancelled ones are deleted), each with its attendees.</summary>
    public IReadOnlyList<Meeting> Meetings => _meetings;

    /// <summary>Everyone's day templates and week templates, as saved.</summary>
    public IReadOnlyList<DayTemplate> DayTemplateRows => _days.Values.ToList();
    public IReadOnlyList<WeekTemplate> WeekTemplateRows => _weeks.Values.ToList();

    /// <summary>Everyone's default week rows, as saved.</summary>
    public IReadOnlyList<StaffWeekTemplate> DefaultWeeks => _staffWeeks;

    /// <summary>The stored blocks (leave, training, teaching, the moved lunch), as saved.</summary>
    public IReadOnlyList<CalendarBlock> StoredBlocks => _blocks;

    /// <summary>The dates using a day template of their own (or none), as saved.</summary>
    public IReadOnlyList<CalendarDayOverride> DayOverrides => _overrides;

    /// <summary>Whether someone is away all day (annual leave, a training day): no sign-in, nothing booked.</summary>
    public bool IsOnLeave(Guid staffUserId, DateOnly date) =>
        _blocks.Any(b => b.StaffUserId == staffUserId
                         && UkTime.DateOf(b.StartUtc) == date
                         && (b.ActivityTypeId == _types["Annual leave"].Id || b.ActivityTypeId == _types["Training"].Id)
                         && (b.EndUtc - b.StartUtc).TotalHours >= 9);

    /// <summary>Which day templates suit a kind of visit: everyone's own of the right names (<see cref="DemoTemplates.ForDayKind"/>).</summary>
    public Func<Guid?, bool>? Prefers(DayKind kind)
    {
        if (!DemoTemplates.ForDayKind.TryGetValue(kind, out var names))
            return null;
        var ids = _days.Values.Where(d => names.Contains(d.Name)).Select(d => d.Id).ToHashSet();
        return id => id is { } dayTemplateId && ids.Contains(dayTemplateId);
    }

    /// <summary>Puts a meeting on its attendees' calendars (<see cref="DemoMeetings"/>), so later plans avoid it.</summary>
    public void AddMeeting(Meeting meeting) => _meetings.Add(meeting);

    /// <summary>Takes a cancelled meeting off (the app deletes it).</summary>
    public void RemoveMeeting(Meeting meeting) => _meetings.Remove(meeting);

    /// <summary>Plans everyone's calendars in memory (nothing saved), and builds the schedule allocator from them.</summary>
    public static DemoCalendars Plan(IReadOnlyList<StaffMember> staff, DemoTimeline timeline, Random random)
    {
        var calendars = new DemoCalendars();
        calendars.Build(staff, timeline, random);
        calendars.BuildAllocator(staff, timeline);
        return calendars;
    }

    public static async Task<DemoCalendars> CreateAsync(
        SeedServices services, IReadOnlyList<StaffMember> staff, DemoTimeline timeline, Random random, CancellationToken ct)
    {
        var calendars = Plan(staff, timeline, random);

        await services.Run(async sp =>
        {
            var db = sp.GetRequiredService<MedAppDbContext>();
            db.CalendarActivityTypes.AddRange(calendars._types.Values.Where(t =>
                !CalendarActivityTypeConfiguration.Seeded.Any(s => s.Id == t.Id)));
            db.DayTemplates.AddRange(calendars._days.Values);
            db.WeekTemplates.AddRange(calendars._weeks.Values);
            db.StaffWeekTemplates.AddRange(calendars._staffWeeks);
            db.CalendarBlocks.AddRange(calendars._blocks);
            db.CalendarBlockExceptions.AddRange(calendars._exceptions);
            db.CalendarDayOverrides.AddRange(calendars._overrides);
            db.Meetings.AddRange(calendars._meetings);
            db.ClosedDays.AddRange(DemoClosedDays.Rows(timeline, staff.First(m => m.IsAdminAt(timeline.AppStartUtc)).Id));
            await db.SaveChangesAsync(ct);
        });

        return calendars;
    }

    /// <summary>
    /// Everything on someone's calendar over some dates but appointments, from the rows planned so far, as the app's
    /// repository would load it (<c>ICalendarRepository.GetCalendarDataAsync</c>): their own templates, and the
    /// meetings they're going to.
    /// </summary>
    public CalendarData DataFor(Guid staffUserId, DateOnly from, DateOnly to)
    {
        var (fromUtc, toUtc) = (WorkingHours.DayStartUtc(from), WorkingHours.DayEndUtc(to));
        var days = _days.Values.Where(d => d.OwnerUserId == staffUserId).ToList();
        return new CalendarData(
            staffUserId, from, to,
            _staffWeeks.Where(s => s.StaffUserId == staffUserId).ToList(),
            _weeks.Values.Where(w => w.OwnerUserId == staffUserId).SelectMany(w => w.Days).ToList(),
            days.SelectMany(d => d.Blocks).ToList(),
            _overrides.Where(o => o.StaffUserId == staffUserId).ToList(),
            _exceptions.Where(e => e.StaffUserId == staffUserId).ToList(),
            _blocks.Where(b => b.StaffUserId == staffUserId).ToList(),
            [],
            days.ToDictionary(d => d.Id, d => d.Name))
        {
            Meetings = _meetings
                .Where(m => m.StartUtc < toUtc && m.EndUtc > fromUtc && m.Attendees.Any(a => a.StaffUserId == staffUserId))
                .ToList(),
            ClosedDays = DemoClosedDays.Set,
        };
    }

    private void Build(IReadOnlyList<StaffMember> staff, DemoTimeline timeline, Random random)
    {
        foreach (var seeded in CalendarActivityTypeConfiguration.Seeded)
            _types[seeded.Name] = new CalendarActivityType { Id = seeded.Id, Name = seeded.Name, Colour = seeded.Colour };
        foreach (var (name, colour) in new[]
                 {
                     ("Ward round", "yellow"), ("Teaching", "pink"), ("Supervision", "red"), ("Off duty", "gray"),
                 })
            _types[name] = new CalendarActivityType { Id = Guid.NewGuid(), Name = name, Colour = colour };

        // Rows from before the clinic started, so every past week shows its blocks.
        var from = UkTime.DateOf(timeline.AppStartUtc).AddDays(-60);

        // Everyone's own templates (templates are personal), and their default week from when they started.
        foreach (var member in staff)
        {
            var templates = DemoTemplates.For(member.Seed);
            foreach (var day in templates.Days)
                Day(member, day, from);
            foreach (var week in templates.Weeks)
                Week(member, week, from);

            var start = UkTime.DateOf(member.StartedUtc);
            var end = member.LeftUtc is { } left ? UkTime.DateOf(left) : (DateOnly?)null;

            // The part-time nurse worked full time until 20 weeks ago: two dated rows, as the app keeps history.
            if (member.Kind == StaffKind.PartTimeNurse)
            {
                var changed = timeline.ThisMonday.AddDays(-7 * 20);
                DefaultWeek(member, DemoTemplates.FullTimeWeek, start, changed.AddDays(-1));
                DefaultWeek(member, templates.DefaultWeek, changed, null);
            }
            else
            {
                DefaultWeek(member, templates.DefaultWeek, start, end);
            }
        }

        // The endoscopist moved their list days' lunch later 10 weeks ago: an edit to the day template, kept as the app
        // keeps one (TemplateHistory), so earlier list days still show the earlier lunch.
        LaterLunch(DemoStaff.Get(staff, DemoTemplates.LateLunchUsername), timeline.ThisMonday.AddDays(-7 * 10));

        // Leave in the past: two weeks each for clinicians, at random, as people take it.
        foreach (var member in staff.Where(s => s.Kind is not (StaffKind.NewStarter or StaffKind.BookingCoordinator or StaffKind.ServiceManager)))
        {
            for (var i = 0; i < 2; i++)
            {
                var weeksAgo = random.Next(3, VolumeCurves.HistoryWeeks - 2);
                var monday = timeline.ThisMonday.AddDays(-7 * weeksAgo);
                if (UkTime.ToUtc(monday, TimeOnly.MinValue) < member.StartedUtc
                    || (member.LeftUtc is { } left && UkTime.ToUtc(monday.AddDays(5), TimeOnly.MinValue) > left))
                    continue;
                Leave(member, monday, 5, "Annual leave", member, timeline.NowUtc.AddDays(-7 * weeksAgo - 42));
            }
        }

        var nurse = staff.Single(s => s.Username == "k.ellison");
        var physio = staff.Single(s => s.Kind == StaffKind.Physiotherapist);
        var surgeon = staff.Single(s => s.Username == "d.ashworth");
        var otherSurgeon = staff.Single(s => s.Username == "r.calloway");
        var minorNurse = staff.Single(s => s.Kind == StaffKind.MinorProceduresNurse);
        var lead = staff.Single(s => s.Kind == StaffKind.ClinicalLead);
        var created = timeline.NowUtc.AddDays(-20);

        // A week's annual leave next month.
        var nextMonth = new DateOnly(timeline.Today.Year, timeline.Today.Month, 1).AddMonths(1);
        var leaveMonday = nextMonth.AddDays((8 - (int)nextMonth.DayOfWeek) % 7);
        if (leaveMonday < timeline.Today.AddDays(10))
            leaveMonday = leaveMonday.AddDays(7);
        Leave(nurse, leaveMonday, 5, "Annual leave", nurse, created);

        // A day's leave this week (not the diary day).
        var leaveDay = Enumerable.Range(0, 5).Select(d => timeline.ThisMonday.AddDays(d)).First(d => d != timeline.DiaryDay && d != timeline.Today);
        Leave(physio, leaveDay, 1, "Annual leave", physio, created);

        // A training day next week.
        var nextMonday = timeline.ThisMonday.AddDays(7);
        Leave(surgeon, nextMonday.AddDays(2), 1, "Training", lead, created, "Mandatory training: resuscitation update");

        // Teaching sessions in the coming weeks.
        foreach (var weeks in new[] { 1, 2 })
            Block(otherSurgeon, nextMonday.AddDays(7 * (weeks - 1) + 3), 16, 0, 17, 0, "Teaching", "Junior doctor teaching", otherSurgeon, created);

        // A day using another of their day templates: the nurse covering recovery on a theatre day next week.
        if (!IsOnLeave(nurse.Id, nextMonday.AddDays(1)))
            SetDayTemplate(nurse, nextMonday.AddDays(1), _days[(nurse.Id, DemoTemplates.TheatreDay)].Id);

        // The meetings, round everything above, as each was arranged.
        MeetingPlan = DemoMeetings.Plan(this, staff, timeline);

        // Lunch moved for one day: the template's lunch is left off that date, and a stored block put in its place, in
        // the first free 45 minutes after the department meeting (13:45 if free), else the last free before it. If
        // nothing is free, the lunch stays where it was.
        var lunchDay = NextWorkingDay(timeline.DiaryDay);
        if (TemplateBlockOn(otherSurgeon, lunchDay, "Lunch") is { } lunch)
        {
            var moved = new CalendarBlockException { Id = Guid.NewGuid(), StaffUserId = otherSurgeon.Id, Date = lunchDay, DayTemplateBlockId = lunch.Id };
            _exceptions.Add(moved);
            var starts = Enumerable.Range(0, 10).Select(i => new TimeOnly(13, 45).AddMinutes(15 * i))
                .Concat(Enumerable.Range(1, 7).Select(i => new TimeOnly(13, 0).AddMinutes(-15 * i)));
            if (!starts.Any(start => Block(otherSurgeon, lunchDay, start, start.AddMinutes(45), "Lunch", "Moved for the department meeting",
                    otherSurgeon, timeline.NowUtc.AddDays(-2))))
                _exceptions.Remove(moved);
        }

        // A template block removed for a day (the nurse's admin time), and an appointment booked in its place. No meeting
        // is there: the block was, when they were arranged.
        var freedDay = PreviousWorkingDay(timeline.DiaryDay);
        if (TemplateBlockOn(minorNurse, freedDay, "Admin time") is { } admin)
        {
            _exceptions.Add(new CalendarBlockException { Id = Guid.NewGuid(), StaffUserId = minorNurse.Id, Date = freedDay, DayTemplateBlockId = admin.Id });
            var start = UkTime.ToUtc(freedDay, admin.StartTime);
            FreedSlot = (minorNurse.Id, start, start.AddMinutes(30));
        }
    }

    private void BuildAllocator(IReadOnlyList<StaffMember> staff, DemoTimeline timeline)
    {
        var from = UkTime.DateOf(timeline.AppStartUtc).AddDays(-60);
        var to = timeline.Today.AddDays(400);

        foreach (var member in staff)
        {
            var data = DataFor(member.Id, from, to);

            foreach (var block in CalendarProjection.TemplateBlocks(data))
                Allocator.AddBlock(member.Id, block.StartUtc, block.EndUtc);
            foreach (var block in data.Blocks)
                Allocator.AddBlock(member.Id, block.StartUtc, block.EndUtc);
            foreach (var meeting in data.Meetings)
                Allocator.AddBlock(member.Id, meeting.StartUtc, meeting.EndUtc);
            for (var date = from; date <= to; date = date.AddDays(1))
                if (CalendarProjection.DayTemplateOn(data, date).DayTemplateId is { } dayTemplateId)
                    Allocator.SetDayTemplate(member.Id, date, dayTemplateId);
        }
    }

    /// <summary>
    /// One of someone's own day templates, its blocks valid from <paramref name="from"/>, checked by the create
    /// command's validator (working hours, 5-minute boundaries, no overlaps).
    /// </summary>
    private void Day(StaffMember owner, DayTemplateSpec spec, DateOnly from)
    {
        var values = spec.Blocks.Select(b => ValueOf(b).Normalised).ToList();
        DayValidator.ValidateAndThrow(new CreateDayTemplateCommand(owner.Id, spec.Name, values, OwnerActor(owner)));

        var day = new DayTemplate { Id = Guid.NewGuid(), OwnerUserId = owner.Id, Name = spec.Name };
        day.Blocks = values.Select(v => v.ToBlock(day.Id, from)).ToList();
        _days[(owner.Id, spec.Name)] = day;
    }

    private static CalendarActor OwnerActor(StaffMember owner) => new(owner.Id, owner.Username, IsAdmin: false);

    private DayTemplateBlockValue ValueOf(BlockSpec block) => new(_types[block.Type].Id, block.From, block.To, block.Note);

    /// <summary>One of someone's own week templates: only their own day templates, as the app allows.</summary>
    private void Week(StaffMember owner, WeekTemplateSpec spec, DateOnly from)
    {
        Guid? Own(int weekday) => spec.MondayToFriday[weekday] is { } name ? _days[(owner.Id, name)].Id : null;
        var command = new CreateWeekTemplateCommand(owner.Id, spec.Name, Own(0), Own(1), Own(2), Own(3), Own(4), OwnerActor(owner));
        WeekValidator.ValidateAndThrow(command);

        var week = new WeekTemplate { Id = Guid.NewGuid(), OwnerUserId = owner.Id, Name = spec.Name };
        week.Days = command.Days().Select(d => d.ToDay(week.Id, from)).ToList();
        _weeks[(owner.Id, spec.Name)] = week;
    }

    private void DefaultWeek(StaffMember member, string week, DateOnly from, DateOnly? to) =>
        _staffWeeks.Add(new StaffWeekTemplate
        {
            Id = Guid.NewGuid(), StaffUserId = member.Id, WeekTemplateId = _weeks[(member.Id, week)].Id, ValidFrom = from, ValidTo = to,
        });

    /// <summary>
    /// The endoscopist's list-day lunch moved later from <paramref name="changedOn"/>, as editing the day template that
    /// day saves it (<see cref="TemplateHistory.Replace{TRow,TValue}"/>): the old lunch's row ends the day before, and a
    /// new one starts that day; the other blocks' rows are kept.
    /// </summary>
    private void LaterLunch(StaffMember member, DateOnly changedOn)
    {
        var template = _days[(member.Id, DemoTemplates.EndoscopyList)];
        var before = ValueOf(DemoTemplates.EndoscopyLunchBefore).Normalised;
        var wanted = template.Blocks.Select(b => DayTemplateBlockValue.Of(b).Normalised)
            .Select(v => v == before ? ValueOf(DemoTemplates.EndoscopyLunchAfter).Normalised : v)
            .ToList();

        var change = TemplateHistory.Replace(template.Blocks, wanted, changedOn, DayTemplateBlockValue.Of,
            (value, validFrom) => value.ToBlock(template.Id, validFrom));
        foreach (var row in change.Close)
            row.ValidTo = change.ClosedValidTo;
        template.Blocks.RemoveAll(change.Delete.Contains);
        template.Blocks.AddRange(change.Add);
    }

    /// <summary>
    /// Whole working days blocked (leave, training), one block a day, as the app lets someone record them: the day is
    /// set to no day template first (its lunch and other template blocks would overlap a whole-day block), then
    /// blocked.
    /// </summary>
    private void Leave(StaffMember member, DateOnly first, int days, string type, StaffMember by, DateTime createdUtc, string note = "")
    {
        for (var date = first; date < first.AddDays(days); date = date.AddDays(1))
        {
            if (!DemoClosedDays.IsWorkingDay(date))
                continue;
            SetDayTemplate(member, date, null);
            Block(member, date, 8, 0, 18, 0, type, note, by, createdUtc);
        }
    }

    /// <summary>A date's own day template (null for none), replacing any it had: one override per person and date.</summary>
    private void SetDayTemplate(StaffMember member, DateOnly date, Guid? dayTemplateId)
    {
        _overrides.RemoveAll(o => o.StaffUserId == member.Id && o.Date == date);
        _overrides.Add(new CalendarDayOverride { Id = Guid.NewGuid(), StaffUserId = member.Id, Date = date, DayTemplateId = dayTemplateId });
    }

    private bool Block(StaffMember member, DateOnly date, int fromHour, int fromMinute, int toHour, int toMinute,
        string type, string note, StaffMember by, DateTime createdUtc) =>
        Block(member, date, new TimeOnly(fromHour, fromMinute), new TimeOnly(toHour, toMinute), type, note, by, createdUtc);

    /// <summary>
    /// Stores a block, if the app would allow it: checked against everything on that day of the person's calendar
    /// (stored blocks and the day template's blocks), as <c>CreateCalendarBlockHandler</c> checks a new one, so one
    /// already there wins. Whether it was stored.
    /// </summary>
    private bool Block(StaffMember member, DateOnly date, TimeOnly from, TimeOnly to,
        string type, string note, StaffMember by, DateTime createdUtc)
    {
        var start = UkTime.ToUtc(date, from);
        var end = UkTime.ToUtc(date, to);
        if (CalendarClashes.Find(DataFor(member.Id, date, date), start, end) is not null)
            return false;

        _blocks.Add(new CalendarBlock
        {
            Id = Guid.NewGuid(),
            StaffUserId = member.Id,
            ActivityTypeId = _types[type].Id,
            StartUtc = start,
            EndUtc = end,
            Note = note,
            CreatedAtUtc = createdUtc,
            CreatedByUserId = by.Id,
            LastUpdated = createdUtc,
        });
        return true;
    }

    /// <summary>The block of a type that someone's template puts on a date, if any.</summary>
    private DayTemplateBlock? TemplateBlockOn(StaffMember member, DateOnly date, string type) =>
        CalendarProjection.TemplateBlocks(DataFor(member.Id, date, date))
            .Select(p => p.Source)
            .FirstOrDefault(b => b.ActivityTypeId == _types[type].Id);

    private static DateOnly NextWorkingDay(DateOnly date)
    {
        do date = date.AddDays(1);
        while (!DemoClosedDays.IsWorkingDay(date));
        return date;
    }

    private static DateOnly PreviousWorkingDay(DateOnly date)
    {
        do date = date.AddDays(-1);
        while (!DemoClosedDays.IsWorkingDay(date));
        return date;
    }
}
