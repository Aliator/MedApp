using FluentValidation;

namespace MedApp.Application.Calendar.Common;

/// <summary>A week template to add or to replace one with: its name and a day template (or none) for each weekday.</summary>
public interface IWeekTemplateCommand
{
    string Name { get; }
    Guid? Monday { get; }
    Guid? Tuesday { get; }
    Guid? Wednesday { get; }
    Guid? Thursday { get; }
    Guid? Friday { get; }
}

public static class WeekTemplateCommands
{
    /// <summary>The weekdays given a day template.</summary>
    public static IReadOnlyList<WeekTemplateDayValue> Days(this IWeekTemplateCommand command) =>
        WeekTemplateDayValue.From(command.Monday, command.Tuesday, command.Wednesday, command.Thursday, command.Friday);
}

/// <summary>The rules for a week template's name.</summary>
public abstract class WeekTemplateValidation<T> : AbstractValidator<T> where T : IWeekTemplateCommand
{
    protected WeekTemplateValidation()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .MaximumLength(CalendarLimits.MaxTemplateNameLength)
            .WithMessage($"Name must not exceed {CalendarLimits.MaxTemplateNameLength} characters.");
    }
}
