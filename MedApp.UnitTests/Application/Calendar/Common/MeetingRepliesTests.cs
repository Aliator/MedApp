using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class MeetingRepliesTests
{
    /// <summary>The MDT, 10:00 to 11:00 next Monday, for both members of staff (the admin arranged it, and isn't going).</summary>
    private static Meeting Mdt() => NewMeeting(At(NextMonday, 10), At(NextMonday, 11));

    /// <summary>A reply made looking at the meeting as it is now.</summary>
    private static MeetingReply AsSeen(Meeting meeting, Guid staffUserId, MeetingReply reply, DateTime nowUtc) =>
        MeetingReplies.Reply(meeting, staffUserId, reply, meeting.StartUtc, meeting.EndUtc, nowUtc);

    private static MeetingAttendee Attendee(Meeting meeting, Guid staffUserId) =>
        meeting.Attendees.Single(a => a.StaffUserId == staffUserId);

    [Test]
    public void ReplyStoredNumbers_NeverChange()
    {
        ((int)MeetingReply.NoReply).Should().Be(0);
        ((int)MeetingReply.Accepted).Should().Be(1);
        ((int)MeetingReply.Declined).Should().Be(2);
    }

    [TestCase(MeetingReply.Accepted, true)]
    [TestCase(MeetingReply.Declined, true)]
    [TestCase(MeetingReply.NoReply, false)]
    public void IsReply_AcceptedOrDeclinedOnly(MeetingReply reply, bool isReply)
    {
        MeetingReplies.IsReply(reply).Should().Be(isReply);
    }

    [TestCase(MeetingReply.Accepted)]
    [TestCase(MeetingReply.Declined)]
    public void Reply_Attendee_RecordsItAndWhen_LeavingEveryoneElseAndTheMeetingAsTheyWere(MeetingReply reply)
    {
        var meeting = Mdt();
        var lastUpdated = meeting.LastUpdated;

        var before = AsSeen(meeting, StaffUserId, reply, Now);

        before.Should().Be(MeetingReply.NoReply);
        Attendee(meeting, StaffUserId).Reply.Should().Be(reply);
        Attendee(meeting, StaffUserId).RepliedAtUtc.Should().Be(Now);
        Attendee(meeting, OtherStaffUserId).Reply.Should().Be(MeetingReply.NoReply);
        Attendee(meeting, OtherStaffUserId).RepliedAtUtc.Should().BeNull();
        meeting.LastUpdated.Should().Be(lastUpdated, "a reply isn't a change to the meeting");
    }

    [Test]
    public void Reply_ChangedMind_RecordsTheNewReplyAndReturnsTheOld()
    {
        var meeting = Mdt();
        AsSeen(meeting, StaffUserId, MeetingReply.Declined, Now.AddHours(-1));

        AsSeen(meeting, StaffUserId, MeetingReply.Accepted, Now).Should().Be(MeetingReply.Declined);

        Attendee(meeting, StaffUserId).Reply.Should().Be(MeetingReply.Accepted);
        Attendee(meeting, StaffUserId).RepliedAtUtc.Should().Be(Now);
    }

    [Test]
    public void Reply_TheSameAgain_ChangesNothing()
    {
        var meeting = Mdt();
        AsSeen(meeting, StaffUserId, MeetingReply.Declined, Now.AddHours(-1));

        AsSeen(meeting, StaffUserId, MeetingReply.Declined, Now).Should().Be(MeetingReply.Declined);

        Attendee(meeting, StaffUserId).RepliedAtUtc.Should().Be(Now.AddHours(-1));
    }

    [Test]
    public void Reply_SomeoneNotGoing_IsForbidden()
    {
        // The admin who arranged it isn't going, so can't reply, for themselves or anyone else.
        var act = () => AsSeen(Mdt(), AdminUserId, MeetingReply.Declined, Now);

        act.Should().Throw<ForbiddenException>().WithMessage(MeetingReplies.NotAnAttendee);
    }

    [TestCase(0)]
    [TestCase(1)]
    public void Reply_FromItsEnd_IsAConflict(int minutesAfterEnd)
    {
        var meeting = NewMeeting(Now.AddMinutes(-60 - minutesAfterEnd), Now.AddMinutes(-minutesAfterEnd));

        var act = () => AsSeen(meeting, StaffUserId, MeetingReply.Accepted, Now);

        act.Should().Throw<ConflictException>().WithMessage(MeetingReplies.Ended);
    }

    [Test]
    public void Reply_UnderWay_IsRecorded()
    {
        var meeting = NewMeeting(Now.AddMinutes(-30), Now.AddMinutes(30));

        AsSeen(meeting, StaffUserId, MeetingReply.Declined, Now);

        Attendee(meeting, StaffUserId).Reply.Should().Be(MeetingReply.Declined);
    }

    [Test]
    public void Reply_NoReply_IsntOne()
    {
        var act = () => AsSeen(Mdt(), StaffUserId, MeetingReply.NoReply, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void ReplyOfAndHasDeclined_ReadThePersonsOwnReply()
    {
        var meeting = Mdt();
        AsSeen(meeting, StaffUserId, MeetingReply.Declined, Now);

        MeetingReplies.ReplyOf(meeting, StaffUserId).Should().Be(MeetingReply.Declined);
        MeetingReplies.ReplyOf(meeting, OtherStaffUserId).Should().Be(MeetingReply.NoReply);
        MeetingReplies.ReplyOf(meeting, AdminUserId).Should().BeNull();
        MeetingReplies.HasDeclined(meeting, StaffUserId).Should().BeTrue();
        MeetingReplies.HasDeclined(meeting, OtherStaffUserId).Should().BeFalse();
        MeetingReplies.HasDeclined(meeting, AdminUserId).Should().BeFalse();
    }

    [Test]
    public void WhyCantReply_GoingAndBeforeItEnds_IsNothing()
    {
        MeetingReplies.WhyCantReply(Mdt(), StaffUserId, Now).Should().BeNull();
        MeetingReplies.CanReply(Mdt(), StaffUserId, Now).Should().BeTrue();
        MeetingReplies.CanReply(Mdt(), AdminUserId, Now).Should().BeFalse();
    }

    [Test]
    public void ResetAfterMove_PutsEveryoneBackToNoReply()
    {
        var meeting = Mdt();
        AsSeen(meeting, StaffUserId, MeetingReply.Declined, Now);
        AsSeen(meeting, OtherStaffUserId, MeetingReply.Accepted, Now);

        MeetingReplies.ResetAfterMove(meeting);

        meeting.Attendees.Should().OnlyContain(a => a.Reply == MeetingReply.NoReply && a.RepliedAtUtc == null);
    }

    [Test]
    public void ResetAfterMove_KeepsTheDeclinesOfThoseBusyAtTheNewTime_Only()
    {
        var meeting = NewMeeting(At(NextMonday, 10), At(NextMonday, 11), [StaffUserId, OtherStaffUserId, AdminUserId]);
        AsSeen(meeting, StaffUserId, MeetingReply.Declined, Now);
        AsSeen(meeting, OtherStaffUserId, MeetingReply.Declined, Now);
        AsSeen(meeting, AdminUserId, MeetingReply.Accepted, Now);

        // AdminUserId is listed but accepted: only a decline is ever kept.
        MeetingReplies.ResetAfterMove(meeting, new HashSet<Guid> { StaffUserId, AdminUserId });

        Attendee(meeting, StaffUserId).Reply.Should().Be(MeetingReply.Declined);
        Attendee(meeting, StaffUserId).RepliedAtUtc.Should().Be(Now);
        Attendee(meeting, OtherStaffUserId).Reply.Should().Be(MeetingReply.NoReply);
        Attendee(meeting, AdminUserId).Reply.Should().Be(MeetingReply.NoReply);
        MeetingReplies.Decliners(meeting).Should().BeEquivalentTo([StaffUserId]);
    }

    [TestCase(10, 30, 11, 0)]
    [TestCase(10, 0, 11, 30)]
    [TestCase(14, 0, 15, 0)]
    public void Reply_TheMeetingMovedSinceTheyLooked_IsAConflictNamingTheNewTime_AndChangesNothing(
        int startHour, int startMinute, int endHour, int endMinute)
    {
        // They saw 10:00 to 11:00; it's now somewhere else.
        var meeting = NewMeeting(At(NextMonday, startHour, startMinute), At(NextMonday, endHour, endMinute));

        var act = () => MeetingReplies.Reply(meeting, StaffUserId, MeetingReply.Accepted, At(NextMonday, 10), At(NextMonday, 11), Now);

        act.Should().Throw<ConflictException>().WithMessage(
            $"This meeting has moved to {UkTime.DescribeRange(meeting.StartUtc, meeting.EndUtc)}: look at its new time, then reply.");
        Attendee(meeting, StaffUserId).Reply.Should().Be(MeetingReply.NoReply);
    }

    [Test]
    public void Reply_TheTimeSeenWithoutAKind_IsReadAsUtc()
    {
        var meeting = Mdt();

        MeetingReplies.Reply(meeting, StaffUserId, MeetingReply.Accepted,
            DateTime.SpecifyKind(meeting.StartUtc, DateTimeKind.Unspecified), meeting.EndUtc, Now);

        Attendee(meeting, StaffUserId).Reply.Should().Be(MeetingReply.Accepted);
    }

    [Test]
    public void Reply_SomeoneNotGoing_IsForbiddenEvenIfItMoved()
    {
        var act = () => MeetingReplies.Reply(Mdt(), AdminUserId, MeetingReply.Accepted, At(NextMonday, 14), At(NextMonday, 15), Now);

        act.Should().Throw<ForbiddenException>();
    }
}
