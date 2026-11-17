using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;

public sealed class CreateDayTemplateValidator : DayTemplateValidation<CreateDayTemplateCommand>
{
    public CreateDayTemplateValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEmpty().WithMessage("Member of staff is required.");
    }
}
