using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Meetings.Commands.ReplyToMeeting;

public sealed class ReplyToMeetingValidator : AbstractValidator<ReplyToMeetingCommand>
{
    public ReplyToMeetingValidator()
    {
        RuleFor(x => x.MeetingId).NotEmpty().WithMessage("Meeting id is required.");

        // By name only, and never back to no reply.
        RuleFor(x => x.Reply)
            .Must(reply => PathwayEnums.TryParse<MeetingReply>(reply, out var parsed) && MeetingReplies.IsReply(parsed))
            .WithMessage(MeetingReplies.NotAReply);

        // Without them, a reply from a screen opened before a move would count for a time the person never saw.
        RuleFor(x => x.ExpectedStartUtc).NotNull().WithMessage(MeetingReplies.NoTimeSeen);
        RuleFor(x => x.ExpectedEndUtc).NotNull().WithMessage(MeetingReplies.NoTimeSeen);
    }
}
