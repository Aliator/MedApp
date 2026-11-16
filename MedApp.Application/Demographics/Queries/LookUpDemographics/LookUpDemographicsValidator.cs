using FluentValidation;
using MedApp.Application.Patients.Common;

namespace MedApp.Application.Demographics.Queries.LookUpDemographics;

public sealed class LookUpDemographicsValidator : AbstractValidator<LookUpDemographicsQuery>
{
    public const string Required = "Enter the NHS number to look up.";

    public LookUpDemographicsValidator()
    {
        RuleFor(x => x.NhsNumber)
            .Must(number => !string.IsNullOrWhiteSpace(number))
            .WithMessage(Required)
            .NhsNumber();
    }
}
