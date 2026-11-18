using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Domain.Mdt;

namespace MedApp.Application.Mdt.Commands.RecordMdtOutcome;

public sealed class RecordMdtOutcomeValidator : AbstractValidator<RecordMdtOutcomeCommand>
{
    public const string NobodyPresent = "Choose who was present.";

    public RecordMdtOutcomeValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("MDT item id is required.");
        RuleFor(x => x.Outcome)
            .Must(o => !string.IsNullOrWhiteSpace(o))
            .WithMessage("Write the team's outcome.")
            .MaximumLength(MdtItem.MaxOutcomeLength)
            .WithMessage($"The outcome must not exceed {MdtItem.MaxOutcomeLength} characters.");
        RuleFor(x => x.PresentUserIds)
            .Cascade(CascadeMode.Stop)
            .Must(ids => ids is { Count: > 0 })
            .WithMessage(NobodyPresent)
            .Must(ids => ids.Distinct().Count() <= MeetingLimits.MaxAttendees)
            .WithMessage($"At most {MeetingLimits.MaxAttendees} people can be present.")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("Present user ids must not be empty.");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Give the version of the MDT item you changed.");
    }
}
