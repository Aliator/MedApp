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
    public static WorkingDays WorkingDays { get; } = new(new InMemoryClosedDays());

    /// <summary>Monday to Friday, and not a bank holiday.</summary>
    public static bool IsWorkingDay(DateOnly date) => WorkingHours.IsWorkingDay(date, Set);

    /// <summary>Why something can't be at these times (a weekend, a bank holiday, outside 08:00 to 18:00), or null.</summary>
    public static string? WhyOutside(DateTime startUtc, DateTime endUtc) => WorkingHours.WhyOutside(startUtc, endUtc, Set);

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
}
