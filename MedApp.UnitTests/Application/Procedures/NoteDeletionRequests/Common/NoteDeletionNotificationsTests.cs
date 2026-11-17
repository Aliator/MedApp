using FluentAssertions;
using MedApp.Application.Procedures.NoteDeletionRequests.Common;
using MedApp.Domain.Notifications;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.NoteDeletionRequests.Common;

[TestFixture]
public sealed class NoteDeletionNotificationsTests
{
    private const string Canary = "canary-text-7f3a";

    private static ProcedureNoteDeletionRequest Request(out PatientProcedure procedure)
    {
        procedure = CreateProcedure(title: Canary);
        var note = procedure.AddNote(text: Canary);
        var request = procedure.RequestDeletion(note);
        request.Reason = Canary;
        return request;
    }

    [TestCase(NotificationKind.NoteDeletionRequested, "Note deletion requested")]
    [TestCase(NotificationKind.NoteDeletionApproved, "Note deleted")]
    [TestCase(NotificationKind.NoteDeletionDeclined, "Note kept")]
    public void TitleOf_EachKind_IsAFixedPhrase(NotificationKind kind, string title)
    {
        NoteDeletionNotifications.TitleOf(kind).Should().Be(title);
        Notification.HasFixedTitle(kind).Should().BeTrue("anonymising leaves fixed titles as they are");
    }

    [Test]
    public void TitleOf_AnotherKind_Throws()
    {
        var act = () => NoteDeletionNotifications.TitleOf(NotificationKind.ProcedureAssigned);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void AfterRequesting_TellsEachAdminOnceButNotTheRequester()
    {
        var request = Request(out var procedure);

        var sent = NoteDeletionNotifications.AfterRequesting(request, [Admin.UserId, OtherUserId, Admin.UserId, Staff.UserId], Now);

        sent.Select(n => n.UserId).Should().BeEquivalentTo([Admin.UserId, OtherUserId]);
        sent.Should().AllSatisfy(n => n.Should().BeEquivalentTo(new
        {
            Kind = NotificationKind.NoteDeletionRequested,
            Title = "Note deletion requested",
            Message = $"{Staff.Username} asked to delete a note on a procedure.",
            ProcedureId = (Guid?)procedure.Id,
            NoteDeletionRequestId = (Guid?)request.Id,
            AppointmentId = (Guid?)null,
            CreatedAtUtc = Now,
            ReadAtUtc = (DateTime?)null
        }));
        sent.Select(n => n.Id).Should().OnlyHaveUniqueItems();
    }

    [TestCase(NoteDeletionRequestStatus.Approved, NotificationKind.NoteDeletionApproved, "test.admin deleted the note you asked to delete.")]
    [TestCase(NoteDeletionRequestStatus.Declined, NotificationKind.NoteDeletionDeclined, "test.admin kept the note you asked to delete.")]
    public void AfterDeciding_TellsTheRequesterTheOutcome(NoteDeletionRequestStatus outcome, NotificationKind kind, string message)
    {
        var request = Request(out var procedure);
        request.Close(outcome, Admin.UserId, Admin.Username, Now);

        var told = NoteDeletionNotifications.AfterDeciding(request, Now);

        told.Should().BeEquivalentTo(new
        {
            UserId = Staff.UserId,
            Kind = kind,
            Title = NoteDeletionNotifications.TitleOf(kind),
            Message = message,
            ProcedureId = (Guid?)procedure.Id,
            NoteDeletionRequestId = (Guid?)null,
            CreatedAtUtc = Now
        });
    }

    [Test]
    public void AfterDeciding_NoUsernameRecorded_SaysAnAdmin()
    {
        var request = Request(out _);
        request.Close(NoteDeletionRequestStatus.Declined, Admin.UserId, null, Now);

        NoteDeletionNotifications.AfterDeciding(request, Now)!.Message.Should().Be("An admin kept the note you asked to delete.");
    }

    [Test]
    public void AfterDeciding_TheirOwnRequest_TellsNobody()
    {
        var request = Request(out _);
        request.Close(NoteDeletionRequestStatus.Approved, Staff.UserId, Staff.Username, Now);

        NoteDeletionNotifications.AfterDeciding(request, Now).Should().BeNull();
    }

    [TestCase(NoteDeletionRequestStatus.Pending)]
    [TestCase(NoteDeletionRequestStatus.Closed)]
    public void AfterDeciding_NotDecided_TellsNobody(NoteDeletionRequestStatus status)
    {
        var request = Request(out _);
        request.Status = status;

        NoteDeletionNotifications.AfterDeciding(request, Now).Should().BeNull();
    }

    [Test]
    public void EveryMessage_HoldsNoFreeText()
    {
        // The note's text, the procedure's title and the reason are all the canary: no notification may carry it.
        var request = Request(out _);
        var sent = NoteDeletionNotifications.AfterRequesting(request, [Admin.UserId], Now).ToList();
        request.Close(NoteDeletionRequestStatus.Approved, Admin.UserId, Admin.Username, Now);
        sent.Add(NoteDeletionNotifications.AfterDeciding(request, Now)!);
        request.Status = NoteDeletionRequestStatus.Declined;
        sent.Add(NoteDeletionNotifications.AfterDeciding(request, Now)!);

        sent.Should().HaveCount(3).And.AllSatisfy(n =>
        {
            n.Title.Should().NotContain(Canary);
            n.Message.Should().NotContain(Canary);
        });
    }
}
