using FluentValidation;

namespace MedApp.Application.Patients.Commands.UpdatePatient;

public sealed class UpdatePatientValidator : AbstractValidator<UpdatePatientCommand>
{
    public UpdatePatientValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("First name must not be empty.")
            .MaximumLength(100)
            .WithMessage("First name must not exceed 100 characters.")
            .When(x => x.FirstName is not null);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Last name must not be empty.")
            .MaximumLength(100)
            .WithMessage("Last name must not exceed 100 characters.")
            .When(x => x.LastName is not null);

        RuleFor(x => x.DateOfBirth)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must be in the past.")
            .When(x => x.DateOfBirth.HasValue);

        RuleFor(x => x.Email)
            .EmailAddress()
            .WithMessage("Email must be a valid email address.")
            .When(x => x.Email is not null);

        RuleFor(x => x)
            .Must(x =>
                x.FirstName is not null ||
                x.LastName is not null ||
                x.DateOfBirth.HasValue ||
                x.Email is not null)
            .WithMessage("At least one field must be provided.");
    }
}