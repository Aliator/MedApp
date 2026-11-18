using AutoFixture;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Referrals;
using MedApp.UnitTests.Common.Constants;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Commands.UpdatePatientPathway;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.UpdatePatientPathway;

[TestFixture]
public sealed class UpdatePatientPathwayHandlerTests : PathwayHandlerTestBase
{
    private UpdatePatientPathwayHandler Handler => Fixture.Create<UpdatePatientPathwayHandler>();

    [Test]
    public async Task Handle_PathwayDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new UpdatePatientPathwayCommand(Guid.NewGuid(), "x", null, null, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase(Status.Cancelled)]
    [TestCase(Status.Archived)]
    public async Task Handle_PathwayClosedOff_ThrowsConflict(Status status)
    {
        var pathway = GivenPathway(CreatePathway(status));

        var act = () => Handler.Handle(new UpdatePatientPathwayCommand(pathway.Id, "x", null, null, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_FieldsChanged_UpdatesThemAndAuditsWhatChanged()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var command = new UpdatePatientPathwayCommand(pathway.Id, " New title ", Today.AddDays(20), "Urgent", Staff);

        var response = await Handler.Handle(command, CancellationToken.None);

        pathway.Title.Should().Be("New title");
        pathway.Priority.Should().Be(PatientPathwayPriority.Urgent);
        response.DueDate.Should().Be(Today.AddDays(20));
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientPathwayAuditAction.DetailsUpdated);
        entry.Details.Should().Contain("\"title\"").And.Contain("\"priority\"").And.Contain("\"dueDate\"")
            .And.NotContain("\"notes\"");
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_SameTitleWithSpaces_ChangesNothingAndDoesNotSave()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));
        var command = new UpdatePatientPathwayCommand(pathway.Id, $"  {PathwayTitle}  ", null, null, Staff);

        await Handler.Handle(command, CancellationToken.None);

        pathway.Title.Should().Be(PathwayTitle);
        pathway.LastUpdated.Should().Be(Now.AddDays(-7));
        SavedAudit.Should().BeEmpty();
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_NothingChanged_DoesNotSave()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));
        var command = new UpdatePatientPathwayCommand(pathway.Id, PathwayTitle, null, "medium", Staff);

        await Handler.Handle(command, CancellationToken.None);

        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_Edited_ResponseNotesCanBeEditedOnlyByTheirAuthor()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var own = pathway.AddNote(Staff.UserId);
        pathway.AddNote(OtherUserId, createdAt: Now.AddDays(-3));

        var response = await Handler.Handle(new UpdatePatientPathwayCommand(pathway.Id, "Renamed", null, null, Staff), CancellationToken.None);

        response.NoteList.Should().HaveCount(2);
        response.NoteList.Single(n => n.CanEdit).Id.Should().Be(own.Id);
        response.NoteList.Single(n => !n.CanEdit).AuthorUsername.Should().Be(OtherUsername);
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new UpdatePatientPathwayCommand(pathway.Id, "Renamed", null, null, Staff), cts.Token);

        Repository.Verify(r => r.GetForUpdateAsync(pathway.Id, cts.Token), Times.Once);
        VerifySavedOnce(cts.Token);
    }

    /// <summary>A pathway accepted from a referral received 10 days ago, High (28 days): its target date is the clock's.</summary>
    private PatientPathway GivenAcceptedPathway()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, priority: PatientPathwayPriority.High));
        var referral = ReferralsTestConstants.CreateReferral(patient: pathway.Patient, status: ReferralStatus.Accepted);
        referral.ReceivedOn = Today.AddDays(-10);
        WaitingClock.Start(referral, 28);
        referral.PathwayId = pathway.Id;
        pathway.Referral = referral;
        pathway.DueDateUtc = DueDates.EndOfDayUtc(referral.TargetOn);
        return pathway;
    }

    [Test]
    public async Task Handle_TargetDateWhileTheClockSetsIt_IsAValidationErrorAndSavesNothing()
    {
        var pathway = GivenAcceptedPathway();

        var act = () => Handler.Handle(
            new UpdatePatientPathwayCommand(pathway.Id, "Renamed", Today.AddDays(60), null, Staff), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().ErrorMessage
            .Should().Be(WaitingClock.TargetFromClockMessage);
        pathway.Title.Should().Be(PathwayTitle);
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_TheSameTargetDateWhileTheClockSetsIt_IsAccepted()
    {
        var pathway = GivenAcceptedPathway();

        await Handler.Handle(
            new UpdatePatientPathwayCommand(pathway.Id, "Renamed", Today.AddDays(18), null, Staff), CancellationToken.None);

        pathway.Title.Should().Be("Renamed");
    }

    [Test]
    public async Task Handle_TargetDateOnceTheClockHasStopped_CanBeEditedByHand()
    {
        var pathway = GivenAcceptedPathway();
        pathway.Referral!.StoppedOn = Today;

        await Handler.Handle(new UpdatePatientPathwayCommand(pathway.Id, null, Today.AddDays(60), null, Staff), CancellationToken.None);

        DueDates.ToUkDate(pathway.DueDateUtc).Should().Be(Today.AddDays(60));
    }

    [Test]
    public async Task Handle_NewPriority_ReReadsTheClocksTargetAndMovesTheTargetDateWithIt()
    {
        var pathway = GivenAcceptedPathway();

        await Handler.Handle(new UpdatePatientPathwayCommand(pathway.Id, null, null, "Urgent", Staff), CancellationToken.None);

        pathway.Referral!.TargetDays.Should().Be(14);
        DueDates.ToUkDate(pathway.DueDateUtc).Should().Be(Today.AddDays(4));
        var change = SavedChanges.Single();
        change.ReferralEvents.Single().Action.Should().Be(ReferralEventAction.TargetChanged);
        change.AuditEntries.Should().HaveCount(2);
        change.AuditEntries[1].Details.Should().Contain("\"source\":\"clock\"");
    }
}
