using FluentValidation;

namespace MedApp.Application.Calendar.ClosedDays.Queries.GetClosedDayBookings;

public sealed class GetClosedDayBookingsValidator : AbstractValidator<GetClosedDayBookingsQuery>
{
    public GetClosedDayBookingsValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Date is required.");
    }
}
