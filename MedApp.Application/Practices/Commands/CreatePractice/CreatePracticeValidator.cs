using FluentValidation;
using MedApp.Application.Practices.Common;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Practices.Commands.CreatePractice;

public sealed class CreatePracticeValidator : AbstractValidator<CreatePracticeCommand>
{
    public CreatePracticeValidator(IOptions<LettersOptions> options)
    {
        this.PracticeDetails(
            options.Value,
            x => x.Name,
            x => x.OdsCode,
            x => x.AddressLines,
            x => x.Postcode,
            x => x.Phone,
            x => x.SecureEmail);
    }
}
