using FluentAssertions;
using MedApp.Application.Calendar.Common;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class BankHolidaysTests
{
    /// <summary>England and Wales bank holidays as gov.uk publishes them (www.gov.uk/bank-holidays).</summary>
    private static IEnumerable<TestCaseData> GovUk()
    {
        yield return Year(2025,
            ("2025-01-01", "New Year's Day"), ("2025-04-18", "Good Friday"), ("2025-04-21", "Easter Monday"),
            ("2025-05-05", "Early May bank holiday"), ("2025-05-26", "Spring bank holiday"), ("2025-08-25", "Summer bank holiday"),
            ("2025-12-25", "Christmas Day"), ("2025-12-26", "Boxing Day"));
        yield return Year(2026,
            ("2026-01-01", "New Year's Day"), ("2026-04-03", "Good Friday"), ("2026-04-06", "Easter Monday"),
            ("2026-05-04", "Early May bank holiday"), ("2026-05-25", "Spring bank holiday"), ("2026-08-31", "Summer bank holiday"),
            ("2026-12-25", "Christmas Day"), ("2026-12-28", "Boxing Day (substitute day)"));
        yield return Year(2027,
            ("2027-01-01", "New Year's Day"), ("2027-03-26", "Good Friday"), ("2027-03-29", "Easter Monday"),
            ("2027-05-03", "Early May bank holiday"), ("2027-05-31", "Spring bank holiday"), ("2027-08-30", "Summer bank holiday"),
            ("2027-12-27", "Christmas Day (substitute day)"), ("2027-12-28", "Boxing Day (substitute day)"));
        yield return Year(2028,
            ("2028-01-03", "New Year's Day (substitute day)"), ("2028-04-14", "Good Friday"), ("2028-04-17", "Easter Monday"),
            ("2028-05-01", "Early May bank holiday"), ("2028-05-29", "Spring bank holiday"), ("2028-08-28", "Summer bank holiday"),
            ("2028-12-25", "Christmas Day"), ("2028-12-26", "Boxing Day"));
    }

    private static TestCaseData Year(int year, params (string Date, string Name)[] holidays) =>
        new TestCaseData(year, holidays.Select(h => new BankHoliday(DateOnly.Parse(h.Date), h.Name)).ToList())
            .SetArgDisplayNames(year.ToString());

    [TestCaseSource(nameof(GovUk))]
    public void EnglandAndWales_MatchesGovUk(int year, List<BankHoliday> expected)
    {
        BankHolidays.EnglandAndWales(year).Should().Equal(expected);
    }

    [Test]
    public void EnglandAndWales_ChristmasDayOnASunday_TheSubstituteIsTheTuesday()
    {
        // 2022: Boxing Day had the Monday, so Christmas Day's substitute was Tuesday 27 December.
        BankHolidays.EnglandAndWales(2022).TakeLast(2).Should().Equal(
            new BankHoliday(new DateOnly(2022, 12, 26), "Boxing Day"),
            new BankHoliday(new DateOnly(2022, 12, 27), "Christmas Day (substitute day)"));
    }

    [Test]
    public void EnglandAndWales_NewYearsDayOnASunday_TheSubstituteIsTheMonday()
    {
        BankHolidays.EnglandAndWales(2023).First().Should().Be(new BankHoliday(new DateOnly(2023, 1, 2), "New Year's Day (substitute day)"));
    }

    [Test]
    public void EnglandAndWales_EveryYear_EightWeekdaysInOrder()
    {
        for (var year = BankHolidays.MinYear; year <= BankHolidays.MaxYear; year++)
        {
            var holidays = BankHolidays.EnglandAndWales(year);
            holidays.Should().HaveCount(8, year.ToString());
            holidays.Should().OnlyContain(h => h.Date.DayOfWeek != DayOfWeek.Saturday && h.Date.DayOfWeek != DayOfWeek.Sunday, year.ToString());
            holidays.Select(h => h.Date).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        }
    }

    [TestCase(1999)]
    [TestCase(2101)]
    public void EnglandAndWales_YearOutOfRange_Throws(int year)
    {
        var act = () => BankHolidays.EnglandAndWales(year);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestCase(2024, 3, 31)]
    [TestCase(2025, 4, 20)]
    [TestCase(2026, 4, 5)]
    [TestCase(2027, 3, 28)]
    [TestCase(2028, 4, 16)]
    [TestCase(2038, 4, 25)]
    [TestCase(2285, 3, 22)]
    public void EasterSunday_IsTheGregorianDate(int year, int month, int day)
    {
        BankHolidays.EasterSunday(year).Should().Be(new DateOnly(year, month, day));
    }
}
