using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.ClosedDays.Commands.AddBankHolidays;

public sealed class AddBankHolidaysValidator : AbstractValidator<AddBankHolidaysCommand>
{
    public static readonly string YearOutOfRange = $"Year must be {BankHolidays.MinYear} to {BankHolidays.MaxYear}.";

    public AddBankHolidaysValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(BankHolidays.MinYear, BankHolidays.MaxYear)
            .WithMessage(YearOutOfRange);
    }
}
