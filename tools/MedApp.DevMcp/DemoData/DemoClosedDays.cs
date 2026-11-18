using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Domain.Calendar;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The demo centre's closed days: England and Wales bank holidays (<see cref="BankHolidays"/>), saved as closed days
/// for every year the story covers, and kept clear by everything the seed plans (appointments, blocks, meetings,
/// sign-ins), as the app keeps new things off them. The seed's working day checks come here rather than to
/// <see cref="WorkingHours"/> alone.
/// </summary>
public static class DemoClosedDays
{
    /// <summary>Every bank holiday the app can work out, so a check on any date the seed plans knows them.</summary>
    public static ClosedDaySet Set { get; } = new(
        Enumerable.Range(BankHolidays.MinYear, BankHolidays.MaxYear - BankHolidays.MinYear + 1)
            .SelectMany(BankHolidays.EnglandAndWales)
            .Select(h => (h.Date, h.Name)));

    /// <summary>The working day's rules with the bank holidays, for the commands' validators the seed runs in memory.</summary>
    public static WorkingDays WorkingDays { get; } = new(new InMemoryClosedDays(), new DefaultHoursOnly());

    /// <summary>Monday to Friday, and not a bank holiday.</summary>
    public static bool IsWorkingDay(DateOnly date) => WorkingHours.IsWorkingDay(date, Set);

    /// <summary>
    /// Why something can't be at these times (a weekend, a bank holiday, outside 08:00 to 18:00: everyone in the demo
    /// works the default day), or null.
    /// </summary>
    public static string? WhyOutside(DateTime startUtc, DateTime endUtc) =>
        WorkingHours.WhyOutside(startUtc, endUtc, Set)
        ?? WorkingTimeSet.None.For(Guid.Empty, UkTime.DateOf(startUtc), Set).WhyOutside(startUtc, endUtc);

    /// <summary>
    /// The closed days to save: every bank holiday from the year the centre started using MedApp to next year, added by
    /// the lead (an admin throughout) when the centre started.
    /// </summary>
    public static IReadOnlyList<ClosedDay> Rows(DemoTimeline timeline, Guid addedBy) =>
        Enumerable.Range(UkTime.DateOf(timeline.AppStartUtc).Year, timeline.Today.Year - UkTime.DateOf(timeline.AppStartUtc).Year + 2)
            .SelectMany(BankHolidays.EnglandAndWales)
            .Select(h => new ClosedDay
            {
                Id = Guid.NewGuid(),
                Date = h.Date,
                Name = h.Name,
                CreatedAtUtc = timeline.AppStartUtc.AddDays(-14),
                CreatedByUserId = addedBy
            })
            .ToList();

    /// <summary>
    /// Validates a command with a validator whose rules look closed days up (<see cref="WorkingDays"/>), throwing as
    /// the API's pipeline would. The lookups are in memory, so this finishes at once.
    /// </summary>
    public static void Validate<T>(IValidator<T> validator, T command) =>
        validator.ValidateAndThrowAsync(command).GetAwaiter().GetResult();

    /// <summary>The bank holidays, answered from memory, for <see cref="WorkingDays"/>; the seed never changes them this way.</summary>
    private sealed class InMemoryClosedDays : IClosedDayRepository
    {
        public Task<IReadOnlyList<ClosedDay>> GetAsync(DateOnly? from, DateOnly? to, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ClosedDay>>(Set.Dates
                .Where(d => (from is null || d >= from) && (to is null || d <= to))
                .Order()
                .Select(d => new ClosedDay { Id = Guid.Empty, Date = d, Name = Set.NameOn(d)! })
                .ToList());

        public Task<ClosedDay?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<ClosedDay?>(null);

        public Task<IReadOnlyList<ClosedDay>> GetOnAsync(DateOnly date, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ClosedDay>>(
                Set.NameOn(date) is { } name ? [new ClosedDay { Date = date, Name = name }] : []);

        public Task AddAsync(IReadOnlyCollection<ClosedDay> days, CancellationToken ct) => throw new NotSupportedException();

        public Task DeleteAsync(ClosedDay day, CancellationToken ct) => throw new NotSupportedException();
    }

    /// <summary>Nobody in the demo has hours of their own yet: everyone works the default day.</summary>
    private sealed class DefaultHoursOnly : IWorkingHoursRepository
    {
        public Task<WorkingTimeSet> GetAsync(IReadOnlyCollection<Guid> staffUserIds, DateOnly from, DateOnly to, CancellationToken ct) =>
            Task.FromResult(WorkingTimeSet.None);

        public Task<IReadOnlyList<StaffHours>> GetStaffHoursOnAsync(Guid staffUserId, DateOnly date, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StaffHours>>([]);

        public Task<IReadOnlyList<StaffHours>> GetStaffHoursForUpdateAsync(Guid staffUserId, DateOnly today, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task SaveStaffHoursAsync(TemplateHistoryChange<StaffHours> change, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<StaffHours>> GetStaffHoursAtSiteAsync(Guid siteId, DateOnly today, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StaffHours>>([]);

        public Task<IReadOnlyList<Domain.Sites.SiteHours>> GetSiteHoursOnAsync(Guid siteId, DateOnly date, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Domain.Sites.SiteHours>>([]);

        public Task<IReadOnlyList<Domain.Sites.SiteHours>> GetSiteHoursOnAsync(
            IReadOnlyCollection<Guid> siteIds, DateOnly date, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Domain.Sites.SiteHours>>([]);

        public Task<IReadOnlyList<Domain.Sites.SiteHours>> GetSiteHoursForUpdateAsync(Guid siteId, DateOnly today, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task SaveSiteHoursAsync(TemplateHistoryChange<Domain.Sites.SiteHours> change, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
