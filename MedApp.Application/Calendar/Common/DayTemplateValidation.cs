using FluentValidation;

namespace MedApp.Application.Calendar.Common;

/// <summary>A day template to add or to replace one with: its name and blocks.</summary>
public interface IDayTemplateCommand
{
    string Name { get; }
    IReadOnlyList<DayTemplateBlockValue> Blocks { get; }
}

/// <summary>
/// The rules for a day template's name and blocks: at most 30 blocks, each inside the working day on 5-minute
/// boundaries, with a note of at most 200 characters, and none overlapping another.
/// </summary>
public abstract class DayTemplateValidation<T> : AbstractValidator<T> where T : IDayTemplateCommand
{
    protected DayTemplateValidation()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .MaximumLength(CalendarLimits.MaxTemplateNameLength)
            .WithMessage($"Name must not exceed {CalendarLimits.MaxTemplateNameLength} characters.");

        RuleFor(x => x.Blocks)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("Blocks must not be null.")
            .Must(blocks => blocks.Count <= CalendarLimits.MaxDayTemplateBlocks)
            .WithMessage($"A day template can have at most {CalendarLimits.MaxDayTemplateBlocks} blocks.");

        RuleForEach(x => x.Blocks).ChildRules(block =>
        {
            block.RuleFor(b => b.ActivityTypeId).NotEmpty().WithMessage("Activity type is required.");

            block.RuleFor(b => b)
                .Must(b => WorkingHours.WhyOutside(b.StartTime, b.EndTime) is null)
                .WithMessage(b => WorkingHours.WhyOutside(b.StartTime, b.EndTime)!)
                .OverridePropertyName("StartTime");

            block.RuleFor(b => b.Note)
                .MaximumLength(CalendarLimits.MaxNoteLength)
                .WithMessage($"Note must not exceed {CalendarLimits.MaxNoteLength} characters.");
        });

        RuleFor(x => x.Blocks)
            .Custom((blocks, context) =>
            {
                if (blocks is null)
                    return;

                var ordered = blocks.OrderBy(b => b.StartTime).ThenBy(b => b.EndTime).ToList();
                for (var i = 1; i < ordered.Count; i++)
                {
                    var before = ordered[i - 1];
                    var block = ordered[i];
                    if (block.StartTime < before.EndTime)
                    {
                        context.AddFailure(
                            $"Blocks can't overlap: {Describe(before)} overlaps {Describe(block)}.");
                        return;
                    }
                }
            });
    }

    private static string Describe(DayTemplateBlockValue block) => $"{block.StartTime:HH:mm} to {block.EndTime:HH:mm}";
}
