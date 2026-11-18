using FluentValidation;
using MedApp.Application.Sites.Common;

namespace MedApp.Application.Sites.Commands.UpdateSite;

public sealed class UpdateSiteValidator : AbstractValidator<UpdateSiteCommand>
{
    public UpdateSiteValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("Site id is required.");
        RuleFor(x => x.Name).Name(SiteLimits.MaxNameLength);
        RuleFor(x => x.AddressLines).Must(SiteRules.AreAddressLines).WithMessage(SiteRules.AddressLinesMessage);
        RuleFor(x => x.Postcode).Must(SiteRules.IsPostcode).WithMessage(SiteRules.PostcodeMessage);
        RuleFor(x => x.Phone).Must(SiteRules.IsPhone).WithMessage(SiteRules.PhoneMessage);
    }
}
