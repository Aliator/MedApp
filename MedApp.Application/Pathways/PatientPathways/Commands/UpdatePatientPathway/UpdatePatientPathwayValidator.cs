using FluentValidation;
using MedApp.Application.Pathways.Common;

namespace MedApp.Application.Pathways.PatientPathways.Commands.UpdatePatientPathway;

public sealed class UpdatePatientPathwayValidator : AbstractValidator<UpdatePatientPathwayCommand>
{
    /// <summary>The 400 for an edit that still sends the old single notes field (a page loaded before the change).</summary>
    public const string NotesMovedMessage =
        "Notes are now added on the pathway's page, one at a time, and can't be changed here. Reload the page, then add your note there.";

    public UpdatePatientPathwayValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Pathway id is required.");

        RuleFor(x => x.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .When(x => x.Title is not null)
            .WithMessage("Title must not be blank.")
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Priority)
            .Must(PathwayEnums.IsPriority)
            .When(x => x.Priority is not null)
            .WithMessage($"Priority must be one of: {PathwayEnums.PriorityNames}.");

        RuleFor(x => x.Notes)
            .Null()
            .WithMessage(NotesMovedMessage);
    }
}
