using FluentValidation;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Referrals.Commands.AcceptReferral;

public sealed class AcceptReferralValidator : AbstractValidator<AcceptReferralCommand>
{
    public AcceptReferralValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.ReferralId).NotEmpty().WithMessage("Referral id is required.");

        RuleFor(x => x.TemplateId).NotEmpty().WithMessage("Choose the pathway template.");

        RuleFor(x => x.Priority)
            .Must(PathwayEnums.IsPriority)
            .WithMessage($"Choose the pathway's priority: one of {PathwayEnums.PriorityNames}.");

        RuleFor(x => x.DueDate)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Choose the pathway's target date.")
            .Must(dueDate => dueDate >= DueDates.ToUkDate(timeProvider.GetUtcNow().UtcDateTime))
            .WithMessage("The target date must not be in the past.");

        RuleFor(x => x.Title)
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(PatientPathwayNote.MaxTextLength)
            .WithMessage($"Notes must not exceed {PatientPathwayNote.MaxTextLength} characters.");

        RuleFor(x => x.AssigneeUserId)
            .Must(id => id != Guid.Empty)
            .WithMessage("Choose someone to assign, or nobody.");

        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Give the version of the referral you triaged.");
    }
}
