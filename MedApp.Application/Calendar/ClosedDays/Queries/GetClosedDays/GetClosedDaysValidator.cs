using FluentValidation;

namespace MedApp.Application.Calendar.ClosedDays.Queries.GetClosedDays;

public sealed class GetClosedDaysValidator : AbstractValidator<GetClosedDaysQuery>
{
    public const string ToBeforeFrom = "To must be on or after From.";

    public GetClosedDaysValidator()
    {
        RuleFor(x => x.To)
            .Must((x, to) => x.From is null || to is null || to >= x.From)
            .WithMessage(ToBeforeFrom);
    }
}
