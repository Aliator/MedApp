using FluentValidation;
using MedApp.Application.Patients.Commands.CreatePatient;

namespace MedApp.Application.Patients.Commands.UpdatePatient;

public sealed class CreatePatientValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.DateOfBirth)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow));

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}