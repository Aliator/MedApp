namespace MedApp.Client.Models;

public sealed class PartialDate
{
    public int? Year { get; init; }
    public int? Month { get; init; }
    public int? Day { get; init; }

    public bool IsEmpty => Year is null && Month is null && Day is null;

    public bool Matches(DateOnly date)
    {
        if (Year is not null && date.Year != Year) return false;
        if (Month is not null && date.Month != Month) return false;
        if (Day is not null && date.Day != Day) return false;
        return true;
    }

    public static PartialDate Empty { get; } = new();

    public static PartialDate From(int? year, int? month, int? day) => new()
    {
        Year = year > 0 ? year : null,
        Month = month > 0 ? month : null,
        Day = day > 0 ? day : null
    };
}