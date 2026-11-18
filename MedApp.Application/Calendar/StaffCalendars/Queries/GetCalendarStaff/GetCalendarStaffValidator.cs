using FluentValidation;
using MedApp.Application.Common.Identity;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;

public sealed class GetCalendarStaffValidator : AbstractValidator<GetCalendarStaffQuery>
{
    /// <summary>The message for a permission name code doesn't know.</summary>
    public static string UnknownPermission(string name) => $"'{name}' isn't a permission.";

    public GetCalendarStaffValidator()
    {
        RuleFor(x => x.Permission)
            .Must(name => Permissions.TryParse(name, out _))
            .When(x => x.Permission is not null)
            .WithMessage(x => UnknownPermission(x.Permission!));
    }
}
