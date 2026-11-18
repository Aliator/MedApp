using FluentValidation;
using MedApp.Application.Practices.Common;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Practices.Commands.UpdatePractice;

public sealed class UpdatePracticeValidator : AbstractValidator<UpdatePracticeCommand>
{
    public UpdatePracticeValidator(IOptions<LettersOptions> options)
    {
        RuleFor(x => x.PracticeId).NotEmpty().WithMessage("Practice id is required.");

        this.PracticeDetails(
            options.Value,
            x => x.Name,
            x => x.OdsCode,
            x => x.AddressLines,
            x => x.Postcode,
            x => x.Phone,
            x => x.SecureEmail);

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Expected version is required: send the version you last saw.");
    }
}
