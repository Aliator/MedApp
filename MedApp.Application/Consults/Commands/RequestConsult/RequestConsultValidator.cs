using FluentValidation;
using MedApp.Domain.Consults;

namespace MedApp.Application.Consults.Commands.RequestConsult;

public sealed class RequestConsultValidator : AbstractValidator<RequestConsultCommand>
{
    public RequestConsultValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Choose the pathway.");
        RuleFor(x => x.AskedTeamId).NotEmpty().WithMessage("Choose the team to ask.");
        RuleFor(x => x.Question)
            .Must(q => !string.IsNullOrWhiteSpace(q)).WithMessage("Write the question for the team.")
            .Must(q => q is null || q.Trim().Length <= Consult.MaxQuestionLength)
            .WithMessage($"The question must not exceed {Consult.MaxQuestionLength} characters.");
    }
}
