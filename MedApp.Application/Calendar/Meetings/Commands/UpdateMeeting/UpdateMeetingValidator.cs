using FluentValidation;
using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.Meetings.Commands.UpdateMeeting;

public sealed class UpdateMeetingValidator : MeetingValidation<UpdateMeetingCommand>
{
    public UpdateMeetingValidator()
    {
        RuleFor(x => x.MeetingId).NotEmpty().WithMessage("Meeting id is required.");
    }
}
