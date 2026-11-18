using FluentValidation;
using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Common;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Patients.Commands.KeepPatientRecord;

public sealed class KeepPatientRecordValidator : AbstractValidator<KeepPatientRecordCommand>
{
    public const int MaxReasonLength = 500;

    public KeepPatientRecordValidator(TimeProvider timeProvider, IOptions<RetentionOptions> options)
    {
        var maxYears = options.Value.MaxKeepYears;

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Give a reason.")
            .MaximumLength(MaxReasonLength)
            .WithMessage($"The reason must not exceed {MaxReasonLength} characters.");

        RuleFor(x => x.KeepUntil)
            .Must(date => date > Today(timeProvider))
            .WithMessage("The review date must be after today.")
            .Must(date => date <= Today(timeProvider).AddYears(maxYears))
            .WithMessage($"The review date must be no more than {maxYears} year{(maxYears == 1 ? "" : "s")} ahead.");
    }

    private static DateOnly Today(TimeProvider timeProvider) => DueDates.ToUkDate(timeProvider.GetUtcNow().UtcDateTime);
}
