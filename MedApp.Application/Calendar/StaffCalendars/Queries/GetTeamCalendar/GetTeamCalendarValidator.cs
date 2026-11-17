using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetTeamCalendar;

public sealed class GetTeamCalendarValidator : AbstractValidator<GetTeamCalendarQuery>
{
    public GetTeamCalendarValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("Date is required.");

        RuleFor(x => x.StaffUserIds)
            .Must(ids => ids.Distinct().Count() <= MeetingLimits.MaxPeople)
            .WithMessage($"The team view shows at most {MeetingLimits.MaxPeople} people.");
    }
}
