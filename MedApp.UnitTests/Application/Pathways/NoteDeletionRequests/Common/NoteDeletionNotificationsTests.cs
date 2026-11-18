using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.NoteDeletionRequests.Common;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.NoteDeletionRequests.Common;

[TestFixture]
public sealed class NoteDeletionNotificationsTests
{
    private const string Canary = "canary-text-7f3a";

    private static PathwayNoteDeletionRequest Request(out PatientPathway pathway, out PatientPathwayNote note, Guid? author = null)
    {
        pathway = CreatePathway(title: Canary);
        note = pathway.AddNote(author ?? OtherUserId, text: Canary);
        var request = pathway.RequestDeletion(note);
        request.Detail = Canary;
        return request;
    }

    [TestCase(NotificationKind.NoteDeletionRequested, "Note deletion requested")]
    [TestCase(NotificationKind.NoteDeletionApproved, "Note deleted")]
    [TestCase(NotificationKind.NoteDeletionDeclined, "Note kept")]
    [TestCase(NotificationKind.AuthorsNoteDeleted, "Your note was deleted")]
    public void TitleOf_EachKind_IsAFixedPhrase(NotificationKind kind, string title)
    {
        NoteDeletionNotifications.TitleOf(kind).Should().Be(title);
        Notification.HasFixedTitle(kind).Should().BeTrue("anonymising leaves fixed titles as they are");
    }

    [Test]
    public void TitleOf_AnotherKind_Throws()
    {
        var act = () => NoteDeletionNotifications.TitleOf(NotificationKind.PathwayAssigned);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void AfterRequesting_TellsEachAdminOnceButNotTheRequester()
    {
        var request = Request(out var pathway, out _);

        var sent = NoteDeletionNotifications.AfterRequesting(request, [Admin.UserId, OtherUserId, Admin.UserId, Staff.UserId], Now);

        sent.Select(n => n.UserId).Should().BeEquivalentTo([Admin.UserId, OtherUserId]);
        sent.Should().AllSatisfy(n => n.Should().BeEquivalentTo(new
        {
            Kind = NotificationKind.NoteDeletionRequested,
            Title = "Note deletion requested",
            Message = $"{Staff.Username} asked to delete a note on a pathway.",
            PathwayId = (Guid?)pathway.Id,
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
        var request = Request(out var pathway, out _);
        request.Close(outcome, Admin.UserId, Admin.Username, Now);

        var told = NoteDeletionNotifications.AfterDeciding(request, Now);

        told.Should().BeEquivalentTo(new
        {
            UserId = Staff.UserId,
            Kind = kind,
            Title = NoteDeletionNotifications.TitleOf(kind),
            Message = message,
            PathwayId = (Guid?)pathway.Id,
            NoteDeletionRequestId = (Guid?)null,
            CreatedAtUtc = Now
        });
    }

    [Test]
    public void AfterDeciding_NoUsernameRecorded_SaysAnAdmin()
    {
        var request = Request(out _, out _);
        request.Close(NoteDeletionRequestStatus.Declined, Admin.UserId, null, Now);

        NoteDeletionNotifications.AfterDeciding(request, Now)!.Message.Should().Be("An admin kept the note you asked to delete.");
    }

    [Test]
    public void AfterDeciding_TheirOwnRequest_TellsNobody()
    {
        var request = Request(out _, out _);
        request.Close(NoteDeletionRequestStatus.Approved, Staff.UserId, Staff.Username, Now);

        NoteDeletionNotifications.AfterDeciding(request, Now).Should().BeNull();
    }

    [TestCase(NoteDeletionRequestStatus.Pending)]
    [TestCase(NoteDeletionRequestStatus.Closed)]
    public void AfterDeciding_NotDecided_TellsNobody(NoteDeletionRequestStatus status)
    {
        var request = Request(out _, out _);
        request.Status = status;

        NoteDeletionNotifications.AfterDeciding(request, Now).Should().BeNull();
    }

    [Test]
    public void ToAuthor_SomeoneElseDeletedIt_TellsTheAuthor()
    {
        var pathway = CreatePathway();
        var note = pathway.AddNote(OtherUserId);

        NoteDeletionNotifications.ToAuthor(note, null, Admin, Now).Should().BeEquivalentTo(new
        {
            UserId = OtherUserId,
            Kind = NotificationKind.AuthorsNoteDeleted,
            Title = "Your note was deleted",
            Message = $"{Admin.Username} deleted a note you wrote on a pathway.",
            PathwayId = (Guid?)pathway.Id,
            NoteDeletionRequestId = (Guid?)null,
            CreatedAtUtc = Now
        });
    }

    [Test]
    public void ToAuthor_TheyDeletedItThemselves_TellsNobody() =>
        NoteDeletionNotifications.ToAuthor(CreatePathway().AddNote(Admin.UserId), null, Admin, Now).Should().BeNull();

    [Test]
    public void ToAuthor_NoAuthorRecorded_TellsNobody()
    {
        var note = new PatientPathwayNote { Id = Guid.NewGuid(), AuthorUserId = null, Text = NoteText, Version = 1 };

        NoteDeletionNotifications.ToAuthor(note, null, Admin, Now).Should().BeNull();
    }

    [Test]
    public void ToAuthor_TheyAskedForIt_TellsThemOnlyAsTheRequester()
    {
        var request = Request(out _, out var note, author: Staff.UserId);
        request.Close(NoteDeletionRequestStatus.Approved, Admin.UserId, Admin.Username, Now);

        NoteDeletionNotifications.ToAuthor(note, request, Admin, Now).Should().BeNull();
        NoteDeletionNotifications.AfterDeleting(note, request, Admin, Now)
            .Should().ContainSingle().Which.Kind.Should().Be(NotificationKind.NoteDeletionApproved);
    }

    [Test]
    public void EveryMessage_HoldsNoFreeText()
    {
        // The note's text, the pathway's title and the detail are all the canary: no notification may carry it.
        var request = Request(out _, out var note);
        var sent = NoteDeletionNotifications.AfterRequesting(request, [Admin.UserId], Now).ToList();
        request.Close(NoteDeletionRequestStatus.Approved, Admin.UserId, Admin.Username, Now);
        sent.AddRange(NoteDeletionNotifications.AfterDeleting(note, request, Admin, Now));
        request.Status = NoteDeletionRequestStatus.Declined;
        sent.Add(NoteDeletionNotifications.AfterDeciding(request, Now)!);

        sent.Should().HaveCount(4).And.AllSatisfy(n =>
        {
            n.Title.Should().NotContain(Canary);
            n.Message.Should().NotContain(Canary);
        });
    }
}
