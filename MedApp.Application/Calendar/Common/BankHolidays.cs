namespace MedApp.Application.Calendar.Common;

/// <summary>A bank holiday: its date, and its name as gov.uk gives it, "(substitute day)" for one moved off a weekend.</summary>
public sealed record BankHoliday(DateOnly Date, string Name);

/// <summary>
/// England and Wales bank holidays, worked out in code from the rules gov.uk publishes them by, with no call to an
/// outside service: New Year's Day, Good Friday, Easter Monday, the early May bank holiday (the first Monday in May),
/// the spring bank holiday (the last Monday in May), the summer bank holiday (the last Monday in August), Christmas Day
/// and Boxing Day. One that falls at a weekend gets a substitute day, the next weekday not already a bank holiday.
/// <para>
/// One-off changes (a royal wedding, a coronation, a jubilee's moved spring bank holiday, VE Day's moved early May bank
/// holiday in 2020) aren't rules, so they're not here: admins add or move those by hand.
/// </para>
/// </summary>
public static class BankHolidays
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    /// <summary>A year's England and Wales bank holidays, by date.</summary>
    public static IReadOnlyList<BankHoliday> EnglandAndWales(int year)
    {
        if (year is < MinYear or > MaxYear)
            throw new ArgumentOutOfRangeException(nameof(year), year, $"The year must be {MinYear} to {MaxYear}.");

        var easter = EasterSunday(year);
        var holidays = new List<BankHoliday>
        {
            new(easter.AddDays(-2), "Good Friday"),
            new(easter.AddDays(1), "Easter Monday"),
            new(FirstMonday(year, 5), "Early May bank holiday"),
            new(LastMonday(year, 5), "Spring bank holiday"),
            new(LastMonday(year, 8), "Summer bank holiday")
        };

        BankHoliday[] fixedDays =
        [
            new(new DateOnly(year, 1, 1), "New Year's Day"),
            new(new DateOnly(year, 12, 25), "Christmas Day"),
            new(new DateOnly(year, 12, 26), "Boxing Day")
        ];

        // Those on a weekday stay where they are; then each on a weekend, in date order, moves to the next weekday
        // that isn't already a bank holiday. So Christmas Day on a Saturday is Monday 27 and Boxing Day (Sunday) is
        // Tuesday 28; Christmas Day on a Sunday is Tuesday 27, as Boxing Day has the Monday.
        holidays.AddRange(fixedDays.Where(h => IsWeekday(h.Date)));
        foreach (var holiday in fixedDays.Where(h => !IsWeekday(h.Date)))
        {
            var substitute = holiday.Date.AddDays(1);
            while (!IsWeekday(substitute) || holidays.Any(h => h.Date == substitute))
                substitute = substitute.AddDays(1);

            holidays.Add(new BankHoliday(substitute, $"{holiday.Name} (substitute day)"));
        }

        return holidays.OrderBy(h => h.Date).ToList();
    }

    /// <summary>Easter Sunday in the Gregorian calendar (the anonymous Gregorian algorithm: Meeus, Jones and Butcher).</summary>
    public static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }

    private static bool IsWeekday(DateOnly date) => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    private static DateOnly FirstMonday(int year, int month)
    {
        var date = new DateOnly(year, month, 1);
        while (date.DayOfWeek != DayOfWeek.Monday)
            date = date.AddDays(1);
        return date;
    }

    private static DateOnly LastMonday(int year, int month)
    {
        var date = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        while (date.DayOfWeek != DayOfWeek.Monday)
            date = date.AddDays(-1);
        return date;
    }
}
