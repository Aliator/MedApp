using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Meetings.Commands.ReplyToMeeting;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Meetings.Commands.ReplyToMeeting;

[TestFixture]
public sealed class ReplyToMeetingValidatorTests
{
    private readonly ReplyToMeetingValidator _validator = new();

    [TestCase("Accepted")]
    [TestCase("Declined")]
    [TestCase("declined")]
    [TestCase(" Accepted ")]
    public void Validate_AReply_IsValid(string reply)
    {
        _validator.Validate(new ReplyToMeetingCommand(MeetingId, reply, StaffActor)).IsValid.Should().BeTrue();
    }

    [TestCase("NoReply")]
    [TestCase("1")]
    [TestCase("2")]
    [TestCase("Maybe")]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase(null)]
    public void Validate_NotAReply_IsInvalid(string? reply)
    {
        var result = _validator.Validate(new ReplyToMeetingCommand(MeetingId, reply!, StaffActor));

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ReplyToMeetingCommand.Reply))
            .Which.ErrorMessage.Should().Be(MeetingReplies.NotAReply);
    }

    [Test]
    public void Validate_NoMeetingId_IsInvalid()
    {
        var result = _validator.Validate(new ReplyToMeetingCommand(Guid.Empty, "Accepted", StaffActor));

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ReplyToMeetingCommand.MeetingId));
    }
}
