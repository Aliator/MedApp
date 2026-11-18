using FluentValidation;
using MedApp.Application.Common.Contacts;
using MedApp.Application.Patients.Common;

namespace MedApp.Application.Patients.Commands.CreatePatient;

public sealed class CreatePatientValidator
    : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("First name must not be empty.")
            .MaximumLength(100)
            .WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Last name must not be empty.")
            .MaximumLength(100)
            .WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.DateOfBirth)
            .Must(d => d != default)
            .WithMessage("Date of birth must be provided.")
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must be in the past.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email must not be empty.")
            .EmailAddress()
            .WithMessage("Email must be a valid email address.");

        RuleFor(x => x.NhsNumber).NhsNumber();

        RuleFor(x => x.AddressLines).AddressLines();
        RuleFor(x => x.Postcode).Postcode();
        RuleFor(x => x.MobilePhone).MobilePhone();
        RuleFor(x => x.PracticeId)
            .Must(id => id != Guid.Empty)
            .WithMessage("Practice id must not be empty: leave it out for none.");
    }
}