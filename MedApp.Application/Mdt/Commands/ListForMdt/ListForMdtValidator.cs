using FluentValidation;
using MedApp.Domain.Mdt;

namespace MedApp.Application.Mdt.Commands.ListForMdt;

public sealed class ListForMdtValidator : AbstractValidator<ListForMdtCommand>
{
    public ListForMdtValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");
        RuleFor(x => x.MeetingId).NotEmpty().WithMessage("Choose an MDT.");
        RuleFor(x => x.Question)
            .Must(q => !string.IsNullOrWhiteSpace(q))
            .WithMessage("Give the question for the team.")
            .MaximumLength(MdtItem.MaxQuestionLength)
            .WithMessage($"The question must not exceed {MdtItem.MaxQuestionLength} characters.");
    }
}
