using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.WeekTemplates.Commands.UpdateWeekTemplate;

public sealed class UpdateWeekTemplateValidator : WeekTemplateValidation<UpdateWeekTemplateCommand>
{
    public UpdateWeekTemplateValidator()
    {
        RuleFor(x => x.WeekTemplateId).NotEmpty().WithMessage("Week template id is required.");
    }
}
