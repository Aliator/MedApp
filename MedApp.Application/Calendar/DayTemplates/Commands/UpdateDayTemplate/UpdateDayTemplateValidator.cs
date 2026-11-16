using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.DayTemplates.Commands.UpdateDayTemplate;

public sealed class UpdateDayTemplateValidator : DayTemplateValidation<UpdateDayTemplateCommand>
{
    public UpdateDayTemplateValidator()
    {
        RuleFor(x => x.DayTemplateId).NotEmpty().WithMessage("Day template id is required.");
    }
}
