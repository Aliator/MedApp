using MedApp.Application.Calendar.Common;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Finds free appointment times on staff calendars, as the app's booking would allow them: inside the working day
/// (08:00 to 18:00 UK time, Monday to Friday, <see cref="WorkingHours"/>), on 5-minute boundaries (starts every
/// 15 minutes, the calendar's rows), and clear of everything already on that person's calendar: template blocks,
/// stored blocks (leave, training, meetings) and appointments. Every time it hands out is reserved, so nothing it
/// books overlaps. Pure, apart from the random choices it's given, so the same seed gives the same diary.
/// </summary>
public sealed class ScheduleAllocator
{
    /// <summary>How far apart the times it offers start: the calendar's rows.</summary>
    public const int StepMinutes = WorkingHours.SlotMinutes;

    private readonly Dictionary<Guid, Dictionary<DateOnly, List<Busy>>> _busy = [];
    private readonly Dictionary<(Guid Staff, DateOnly Date), Guid> _dayTemplates = [];
    private readonly Dictionary<(Guid Staff, DateOnly Date), int> _caps = [];
    private readonly Dictionary<(Guid Staff, DateOnly Date), int> _appointmentCounts = [];
    private readonly List<(Guid Staff, DateOnly Date, Busy Item)> _journal = [];

    /// <summary>Something on a calendar: a block, or an appointment this allocator handed out.</summary>
    public sealed record Busy(DateTime StartUtc, DateTime EndUtc, bool IsAppointment);

    /// <summary>A time it found: free for that person, not yet reserved.</summary>
    public sealed record Slot(Guid StaffUserId, DateTime StartUtc, DateTime EndUtc);

    /// <summary>Puts a block (a template block, leave, a meeting) on someone's calendar: nothing can be booked over it.</summary>
    public void AddBlock(Guid staffUserId, DateTime startUtc, DateTime endUtc) =>
        Add(staffUserId, new Busy(startUtc, endUtc, IsAppointment: false), journal: false);

    /// <summary>Records which day template applies to someone's date, for <see cref="Find"/>'s preference.</summary>
    public void SetDayTemplate(Guid staffUserId, DateOnly date, Guid dayTemplateId) =>
        _dayTemplates[(staffUserId, date)] = dayTemplateId;

    /// <summary>The day template on someone's date, if any.</summary>
    public Guid? DayTemplateOn(Guid staffUserId, DateOnly date) =>
        _dayTemplates.TryGetValue((staffUserId, date), out var id) ? id : null;

    /// <summary>Limits how many appointments <see cref="Find"/> will put on someone's date (e.g. to leave free time).</summary>
    public void SetCap(Guid staffUserId, DateOnly date, int maxAppointments) =>
        _caps[(staffUserId, date)] = maxAppointments;

    /// <summary>How many appointments have been reserved on someone's date.</summary>
    public int AppointmentsOn(Guid staffUserId, DateOnly date) =>
        _appointmentCounts.GetValueOrDefault((staffUserId, date));

    /// <summary>Whether a time is inside the working day and clear of everything on that person's calendar.</summary>
    public bool IsFree(Guid staffUserId, DateTime startUtc, DateTime endUtc)
    {
        if (DemoClosedDays.WhyOutside(startUtc, endUtc) is not null)
            return false;

        var date = UkTime.DateOf(startUtc);
        return !_busy.TryGetValue(staffUserId, out var days)
               || !days.TryGetValue(date, out var items)
               || !items.Any(b => CalendarClashes.Overlaps(startUtc, endUtc, b.StartUtc, b.EndUtc));
    }

    /// <summary>
    /// The first day from <paramref name="earliestUtc"/> with a free time of <paramref name="minutes"/> for this person,
    /// and a time on it chosen at random from the free ones. Days whose day template <paramref name="preferDay"/>
    /// accepts are tried first (minor surgery lists for biopsies, say), within <paramref name="searchDays"/>; then any day;
    /// then further ahead. Now and then (<paramref name="skipChance"/>) a day with room is passed over, as when a
    /// patient can't come on the first date offered. Null if nothing is free within a year.
    /// </summary>
    public Slot? Find(
        Guid staffUserId,
        DateTime earliestUtc,
        int minutes,
        Random random,
        Func<Guid?, bool>? preferDay = null,
        int searchDays = 28,
        double skipChance = 0)
    {
        var first = UkTime.DateOf(earliestUtc);

        if (preferDay is not null && Search(first, searchDays, preferDay, skipChance) is { } preferred)
            return preferred;

        return Search(first, searchDays, null, skipChance)
               ?? Search(first, 366, null, 0);

        Slot? Search(DateOnly from, int days, Func<Guid?, bool>? accept, double skip)
        {
            for (var date = from; date <= from.AddDays(days); date = date.AddDays(1))
            {
                if (!DemoClosedDays.IsWorkingDay(date))
                    continue;
                if (accept is not null && !accept(DayTemplateOn(staffUserId, date)))
                    continue;
                if (_caps.TryGetValue((staffUserId, date), out var cap) && AppointmentsOn(staffUserId, date) >= cap)
                    continue;

                var free = FreeStarts(staffUserId, date, minutes, earliestUtc);
                if (free.Count == 0)
                    continue;
                if (skip > 0 && random.NextDouble() < skip)
                    continue;

                var start = free[random.Next(free.Count)];
                return new Slot(staffUserId, start, start.AddMinutes(minutes));
            }

            return null;
        }
    }

    /// <summary>Every free start on a date, at the calendar's rows, not before <paramref name="notBeforeUtc"/>.</summary>
    public IReadOnlyList<DateTime> FreeStarts(Guid staffUserId, DateOnly date, int minutes, DateTime notBeforeUtc)
    {
        var starts = new List<DateTime>();
        if (!DemoClosedDays.IsWorkingDay(date))
            return starts;

        var dayMinutes = (int)(WorkingHours.End - WorkingHours.Start).TotalMinutes;
        for (var offset = 0; offset + minutes <= dayMinutes; offset += StepMinutes)
        {
            var start = UkTime.ToUtc(date, WorkingHours.Start.AddMinutes(offset));
            var end = start.AddMinutes(minutes);
            if (start >= notBeforeUtc && IsFree(staffUserId, start, end))
                starts.Add(start);
        }

        return starts;
    }

    /// <summary>Books a time: it's busy from now on. Refuses one that isn't free, so nothing can overlap.</summary>
    public void Reserve(Guid staffUserId, DateTime startUtc, DateTime endUtc)
    {
        if (!IsFree(staffUserId, startUtc, endUtc))
            throw new InvalidOperationException(
                $"{UkTime.Describe(startUtc)} to {UkTime.DescribeTime(endUtc)} isn't free on that calendar.");

        Add(staffUserId, new Busy(startUtc, endUtc, IsAppointment: true), journal: true);
    }

    /// <summary>Takes back a reservation (an appointment cancelled or moved): the time is free again.</summary>
    public void Release(Guid staffUserId, DateTime startUtc, DateTime endUtc)
    {
        var date = UkTime.DateOf(startUtc);
        if (!_busy.TryGetValue(staffUserId, out var days) || !days.TryGetValue(date, out var items))
            return;

        var index = items.FindIndex(b => b.IsAppointment && b.StartUtc == startUtc && b.EndUtc == endUtc);
        if (index < 0)
            return;

        items.RemoveAt(index);
        _appointmentCounts[(staffUserId, date)]--;
    }

    /// <summary>A point to go back to with <see cref="Rollback"/>.</summary>
    public int Checkpoint() => _journal.Count;

    /// <summary>Takes back every reservation made since the checkpoint (a pathway replayed again differently).</summary>
    public void Rollback(int checkpoint)
    {
        for (var i = _journal.Count - 1; i >= checkpoint; i--)
        {
            var (staff, date, item) = _journal[i];
            if (_busy[staff][date].Remove(item))
                _appointmentCounts[(staff, date)]--;
        }

        _journal.RemoveRange(checkpoint, _journal.Count - checkpoint);
    }

    /// <summary>Every appointment reserved, by person: for checking that none overlap.</summary>
    public IEnumerable<(Guid StaffUserId, Busy Item)> Reservations() =>
        _busy.SelectMany(staff => staff.Value.Values.SelectMany(items => items)
            .Where(b => b.IsAppointment)
            .Select(b => (staff.Key, b)));

    private void Add(Guid staffUserId, Busy item, bool journal)
    {
        var date = UkTime.DateOf(item.StartUtc);
        if (!_busy.TryGetValue(staffUserId, out var days))
            _busy[staffUserId] = days = [];
        if (!days.TryGetValue(date, out var items))
            days[date] = items = [];

        items.Add(item);
        if (!item.IsAppointment)
            return;

        _appointmentCounts[(staffUserId, date)] = AppointmentsOn(staffUserId, date) + 1;
        if (journal)
            _journal.Add((staffUserId, date, item));
    }
}
