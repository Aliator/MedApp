using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Meetings.Commands.ReplyToMeeting;

public sealed class ReplyToMeetingValidator : AbstractValidator<ReplyToMeetingCommand>
{
    public ReplyToMeetingValidator()
    {
        RuleFor(x => x.MeetingId).NotEmpty().WithMessage("Meeting id is required.");

        // By name only, and never back to no reply.
        RuleFor(x => x.Reply)
            .Must(reply => ProcedureEnums.TryParse<MeetingReply>(reply, out var parsed) && MeetingReplies.IsReply(parsed))
            .WithMessage(MeetingReplies.NotAReply);
    }
}
