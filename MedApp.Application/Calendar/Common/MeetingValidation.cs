using FluentValidation;

namespace MedApp.Application.Calendar.Common;

/// <summary>A meeting to add or to replace one with.</summary>
public interface IMeetingCommand
{
    string Title { get; }
    string? Notes { get; }
    DateTime StartUtc { get; }
    DateTime EndUtc { get; }
    IReadOnlyList<Guid> AttendeeUserIds { get; }

    /// <summary>
    /// Whether it's a team's MDT (multidisciplinary team meeting), which pathways can be listed for; null leaves a
    /// meeting as it is (so a page from before MDTs can't turn one into an ordinary meeting), and adds an ordinary one.
    /// </summary>
    bool? IsMdt { get; }

    /// <summary>The MDT's team: required when <see cref="IsMdt"/>, ignored otherwise.</summary>
    Guid? TeamId { get; }
}

/// <summary>
/// The rules for a meeting: a title of at most 100 characters, notes of at most 500, the working day's rules for its
/// time (one working day, not a weekend or a closed day, 08:00 to 18:00 UK time, on 5-minute boundaries), and at least
/// one attendee.
/// </summary>
public abstract class MeetingValidation<T> : AbstractValidator<T> where T : IMeetingCommand
{
    public const string NoAttendees = "A meeting needs at least one attendee.";

    protected MeetingValidation(WorkingDays workingDays)
    {
        RuleFor(x => x.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Title is required.")
            .MaximumLength(MeetingLimits.MaxTitleLength)
            .WithMessage($"Title must not exceed {MeetingLimits.MaxTitleLength} characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(MeetingLimits.MaxNotesLength)
            .WithMessage($"Notes must not exceed {MeetingLimits.MaxNotesLength} characters.");

        RuleFor(x => x.StartUtc).InsideWorkingDay(x => x.EndUtc, workingDays);

        RuleFor(x => x.AttendeeUserIds)
            .Cascade(CascadeMode.Stop)
            .Must(ids => ids is { Count: > 0 })
            .WithMessage(NoAttendees)
            .Must(ids => ids.Distinct().Count() <= MeetingLimits.MaxAttendees)
            .WithMessage($"A meeting can have at most {MeetingLimits.MaxAttendees} attendees.")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("Attendee ids must not be empty.");

        RuleFor(x => x.TeamId)
            .Must(id => id is not null && id != Guid.Empty)
            .When(x => x.IsMdt == true)
            .WithMessage(MdtNeedsTeam);
    }

    public const string MdtNeedsTeam = "Choose the team whose MDT it is.";
}
