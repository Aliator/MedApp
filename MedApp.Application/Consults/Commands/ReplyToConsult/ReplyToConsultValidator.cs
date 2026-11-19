using FluentValidation;
using MedApp.Domain.Consults;

namespace MedApp.Application.Consults.Commands.ReplyToConsult;

public sealed class ReplyToConsultValidator : AbstractValidator<ReplyToConsultCommand>
{
    public ReplyToConsultValidator()
    {
        RuleFor(x => x.Reply)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Write the reply.")
            .Must(r => r is null || r.Trim().Length <= Consult.MaxReplyLength)
            .WithMessage($"The reply must not exceed {Consult.MaxReplyLength} characters.");
    }
}
