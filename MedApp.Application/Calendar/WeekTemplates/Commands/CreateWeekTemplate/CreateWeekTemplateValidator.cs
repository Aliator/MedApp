using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.WeekTemplates.Commands.CreateWeekTemplate;

public sealed class CreateWeekTemplateValidator : WeekTemplateValidation<CreateWeekTemplateCommand>
{
    public CreateWeekTemplateValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEmpty().WithMessage("Member of staff is required.");
    }
}
