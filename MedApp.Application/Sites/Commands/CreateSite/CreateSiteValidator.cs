using FluentValidation;
using MedApp.Application.Sites.Common;

namespace MedApp.Application.Sites.Commands.CreateSite;

public sealed class CreateSiteValidator : AbstractValidator<CreateSiteCommand>
{
    public CreateSiteValidator()
    {
        RuleFor(x => x.Name).Name(SiteLimits.MaxNameLength);
        RuleFor(x => x.AddressLines).Must(SiteRules.AreAddressLines).WithMessage(SiteRules.AddressLinesMessage);
        RuleFor(x => x.Postcode).Must(SiteRules.IsPostcode).WithMessage(SiteRules.PostcodeMessage);
        RuleFor(x => x.Phone).Must(SiteRules.IsPhone).WithMessage(SiteRules.PhoneMessage);
    }
}
