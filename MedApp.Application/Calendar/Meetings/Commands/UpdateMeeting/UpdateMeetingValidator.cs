using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.Meetings.Commands.UpdateMeeting;

public sealed class UpdateMeetingValidator : MeetingValidation<UpdateMeetingCommand>
{
    public const string NoExpectedLastUpdated = "The meeting's last updated time, as you last saw it, is required.";

    public UpdateMeetingValidator()
    {
        RuleFor(x => x.MeetingId).NotEmpty().WithMessage("Meeting id is required.");

        // Without it, a change made from a stale screen would silently undo someone else's.
        RuleFor(x => x.ExpectedLastUpdated).NotNull().WithMessage(NoExpectedLastUpdated);
    }
}
