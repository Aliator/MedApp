using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.Hours.Commands.SetStaffHours;

public sealed class SetStaffHoursValidator : AbstractValidator<SetStaffHoursCommand>
{
    public const string NoSite = "Each day needs a site.";

    public SetStaffHoursValidator()
    {
        RuleFor(x => x.StaffUserId).NotEmpty().WithMessage("Member of staff is required.");

        this.Days(x => x.Days);

        RuleForEach(x => x.Days ?? Array.Empty<StaffHoursDay>())
            .Must(d => d.SiteId != Guid.Empty)
            .WithMessage(NoSite)
            .OverridePropertyName("Days");
    }
}
