using FluentAssertions;
using MedApp.Application.Calendar.ClosedDays.Commands.AddBankHolidays;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ClosedDays.Commands.AddBankHolidays;

[TestFixture]
public sealed class AddBankHolidaysValidatorTests
{
    private readonly AddBankHolidaysValidator _validator = new();

    [TestCase(2000)]
    [TestCase(2027)]
    [TestCase(2100)]
    public void Validate_YearInRange_Passes(int year)
    {
        _validator.Validate(new AddBankHolidaysCommand(year, AdminActor)).IsValid.Should().BeTrue();
    }

    [TestCase(1999)]
    [TestCase(2101)]
    [TestCase(0)]
    public void Validate_YearOutOfRange_Fails(int year)
    {
        _validator.Validate(new AddBankHolidaysCommand(year, AdminActor))
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(AddBankHolidaysValidator.YearOutOfRange);
    }
}
