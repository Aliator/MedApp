using FluentAssertions;
using FluentValidation.TestHelper;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Clinics.Commands.CreateClinicPattern;
using MedApp.Application.Clinics.Common;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.ClinicsTestConstants;

namespace MedApp.UnitTests.Application.Clinics.Common;

[TestFixture]
public sealed class ClinicPatternRulesTests
{
    [Test]
    public void Dates_EveryWeek_EachMondayInTheWeeksAsked()
    {
        // Today is Monday 16 November: it counts, and so do the next two Mondays.
        ClinicPatternRules.Dates(CreatePattern(), Today, 3)
            .Should().Equal(Today, Today.AddDays(7), Today.AddDays(14));
    }

    [Test]
    public void Dates_EveryTwoWeeks_CountedFromTheFirstDayOnOrAfterTheFromDate()
    {
        // From Tuesday 10 November: the first Monday is 16 November, then every second week.
        var pattern = CreatePattern(everyWeeks: 2, fromDate: new DateOnly(2026, 11, 10));

        ClinicPatternRules.Dates(pattern, Today, 6)
            .Should().Equal(Today, Today.AddDays(14), Today.AddDays(28));
    }

    [Test]
    public void Dates_StartedLongAgo_KeepsItsRhythm()
    {
        // Every three weeks from Monday 2 November: 23 November is the next one after it.
        var pattern = CreatePattern(everyWeeks: 3, fromDate: new DateOnly(2026, 11, 2));

        ClinicPatternRules.Dates(pattern, Today, 4).Should().Equal(NextMonday);
    }

    [Test]
    public void Dates_StopAtTheUntilDate_AndStartAtTheFromDate()
    {
        var pattern = CreatePattern(fromDate: NextMonday, untilDate: NextMonday.AddDays(7));

        ClinicPatternRules.Dates(pattern, Today, 13).Should().Equal(NextMonday, NextMonday.AddDays(7));
    }

    [Test]
    public void Dates_EndedPattern_None() =>
        ClinicPatternRules.Dates(CreatePattern(fromDate: LongAgo, untilDate: Today.AddDays(-1)), Today, 4).Should().BeEmpty();

    [Test]
    public void Validator_ValidPattern_Passes() =>
        new CreateClinicPatternValidator().TestValidate(Command()).ShouldNotHaveAnyValidationErrors();

    [Test]
    public void Validator_ChecksEachField()
    {
        var result = new CreateClinicPatternValidator().TestValidate(Command() with
        {
            StaffUserId = Guid.Empty,
            RoomId = Guid.Empty,
            SpecialtyId = Guid.Empty,
            DayOfWeek = "Someday",
            Starts = new TimeOnly(5, 0),
            SlotMinutes = 7,
            EveryWeeks = 5,
            UntilDate = Today.AddDays(-1)
        });

        result.ShouldHaveValidationErrorFor("StaffUserId");
        result.ShouldHaveValidationErrorFor("RoomId");
        result.ShouldHaveValidationErrorFor("SpecialtyId");
        result.ShouldHaveValidationErrorFor("DayOfWeek").WithErrorMessage(HoursValidation.DayOfWeekMessage);
        result.ShouldHaveValidationErrorFor("Starts").WithErrorMessage(WorkingHours.OutsideBounds);
        result.ShouldHaveValidationErrorFor("SlotMinutes").WithErrorMessage(ClinicRules.SlotLength);
        result.ShouldHaveValidationErrorFor("EveryWeeks").WithErrorMessage(ClinicPatternRules.EveryWeeksMessage);
        result.ShouldHaveValidationErrorFor("UntilDate").WithErrorMessage(ClinicPatternRules.UntilBeforeFrom);
    }

    [Test]
    public void Validator_SlotsThatDontDivide_Refused() =>
        new CreateClinicPatternValidator().TestValidate(Command() with { SlotMinutes = 25 })
            .ShouldHaveValidationErrorFor("SlotMinutes").WithErrorMessage(ClinicRules.SlotsDontDivide);

    private static CreateClinicPatternCommand Command() => new(
        StaffUserId, Guid.NewGuid(), SpecialtyId, "Monday", new TimeOnly(9, 0), new TimeOnly(12, 0), 20, 1, Today, null, AdminActor);
}
