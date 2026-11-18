using FluentValidation;
using MedApp.Application.Common.Contacts;
using MedApp.Application.Patients.Common;

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

        RuleFor(x => x.NhsNumber).NhsNumber();

        RuleFor(x => x.AddressLines).AddressLines();
        RuleFor(x => x.Postcode).Postcode();
        RuleFor(x => x.MobilePhone).MobilePhone();
        RuleFor(x => x.ReminderChannel).ReminderChannelName();
        RuleFor(x => x.UnlistedPractice).UnlistedPractice();
        RuleFor(x => x.UnlistedPractice)
            .Null()
            .When(x => x.PracticeId is { } id && id != Guid.Empty)
            .WithMessage(UnlistedPractices.BothMessage);

        RuleFor(x => x)
            .Must(x =>
                x.FirstName is not null ||
                x.LastName is not null ||
                x.DateOfBirth.HasValue ||
                x.Email is not null ||
                x.NhsNumber is not null ||
                x.AddressLines is not null ||
                x.Postcode is not null ||
                x.MobilePhone is not null ||
                x.ReminderChannel is not null ||
                x.PracticeId.HasValue ||
                x.UnlistedPractice is not null)
            .WithMessage("At least one field must be provided.");
    }
}