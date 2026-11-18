using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Sites.Commands.SetSiteHours;

public sealed class SetSiteHoursValidator : AbstractValidator<SetSiteHoursCommand>
{
    public SetSiteHoursValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("Site is required.");

        this.Days(x => x.Days);
    }
}
