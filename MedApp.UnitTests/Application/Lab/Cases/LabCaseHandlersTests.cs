using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Lab.Cases.Commands;
using MedApp.Application.Lab.Cases.Queries;
using MedApp.Application.Lab.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specimens.Commands.MarkSpecimenReceived;
using MedApp.Domain.Activity;
using MedApp.Domain.Lab;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.TheLab.Cases;

[TestFixture]
public sealed class LabCaseStepHandlersTests : LabCaseTestBase
{
    private RecordLabStepHandler Steps => new(CaseAccess, LabCases.Object, Clock);

    private RecordLabOutcomeHandler Outcomes => new(CaseAccess, LabCases.Object, Pathways.Object, Notifications.Object, Clock);

    private MarkLabStepEnteredInErrorHandler StepErrors => new(CaseAccess, LabCases.Object, Clock);

    [Test]
    public async Task RecordStep_TheNextOne_IsRecorded_Audited_AndSaved()
    {
        var labCase = GivenCase(LabPathway());

        var response = await Steps.Handle(new RecordLabStepCommand(labCase.Id, null, "accessioned", null, null, 2, LabMember), Cts.Token);

        response.Steps.Select(s => s.Kind).Should().Equal("Received", "Accessioned");
        response.Next!.Kind.Should().Be("Choice");
        response.Version.Should().Be(3);
        SavedAudit.Should().ContainSingle(e => e.Action == PatientPathwayAuditAction.LabStepRecorded);
        Tokens.Should().Contain(Cts.Token);
    }

    [Test]
    public async Task RecordStep_NotTheNextOne_IsA409_AndNotTheLab_IsA409()
    {
        var labCase = GivenCase(LabPathway());

        var wrong = () => Steps.Handle(new RecordLabStepCommand(labCase.Id, Guid.NewGuid(), null, null, null, 2, LabMember), Cts.Token);
        await wrong.Should().ThrowAsync<ConflictException>().WithMessage("The next step is Accessioned.");

        var clinic = () => Steps.Handle(new RecordLabStepCommand(labCase.Id, null, "Accessioned", null, null, 2, ClinicStaff), Cts.Token);
        await clinic.Should().ThrowAsync<ConflictException>().WithMessage(LabCaseRules.LabWorkMessage);

        var stale = () => Steps.Handle(new RecordLabStepCommand(labCase.Id, null, "Accessioned", null, null, 1, LabMember), Cts.Token);
        await stale.Should().ThrowAsync<ConflictException>().WithMessage(LabCaseRules.ChangedMeanwhileMessage);
    }

    [Test]
    public async Task RecordStep_ASecondOpinion_SaysWhereItWent()
    {
        var labCase = GivenCase(LabPathway(), null, ThroughRequiredSteps());
        labCase.Steps.Add(Step(labCase, 5, LabCaseStepKind.Outcome, choice: LabChoice.NeedsMoreWork, outcome: LabOutcome.SecondOpinion));
        var sent = Histology.Steps.Single(s => s.Kind == LabStepKind.SentForSecondOpinion);

        var act = () => Steps.Handle(new RecordLabStepCommand(labCase.Id, sent.Id, null, null, " ", 2, LabMember), Cts.Token);
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Say where it went for the second opinion.*");

        await Steps.Handle(new RecordLabStepCommand(labCase.Id, sent.Id, null, null, "Synthetic referral centre", 2, LabMember), Cts.Token);
        labCase.Steps.Last().Detail.Should().Be("Synthetic referral centre");
        SavedAudit.Should().AllSatisfy(e => e.Details.Should().NotContain("Synthetic referral centre"));
    }

    [Test]
    public async Task Outcome_NotOffered_IsA400_AndTheWrongChoiceA409()
    {
        var labCase = GivenCase(LabPathway(), null, ThroughRequiredSteps()[..1]);

        var notOffered = () => Outcomes.Handle(new RecordLabOutcomeCommand(labCase.Id, "SampleAdequate", "NoMoreWork", null, 2, LabMember), Cts.Token);
        await notOffered.Should().ThrowAsync<ValidationException>().WithMessage("*Choose the outcome: Adequate, Insufficient: ask for a repeat.*");

        var wrongChoice = () => Outcomes.Handle(new RecordLabOutcomeCommand(labCase.Id, "NeedsMoreWork", "NoMoreWork", null, 2, LabMember), Cts.Token);
        await wrongChoice.Should().ThrowAsync<ConflictException>().WithMessage("Record the outcome of “Sample adequate?” first.");
    }

    [Test]
    public async Task Insufficient_RejectsTheSamples_ReopensTheirStage_AndTellsTheClinic()
    {
        var pathway = LabPathway();
        var labCase = GivenCase(pathway, null, ThroughRequiredSteps()[..1]);
        var sample = labCase.Specimens.Single();
        pathway.StageAt(1).IsLocked = true;

        var missing = () => Outcomes.Handle(new RecordLabOutcomeCommand(labCase.Id, "SampleAdequate", "Insufficient", " ", 2, LabMember), Cts.Token);
        await missing.Should().ThrowAsync<ValidationException>().WithMessage($"*{LabCaseRules.InsufficientDetailMessage}*");

        var response = await Outcomes.Handle(
            new RecordLabOutcomeCommand(labCase.Id, "SampleAdequate", "Insufficient", "Synthetic: too little", 2, LabMember), Cts.Token);

        response.Status.Should().Be("RepeatRequested");
        sample.Status.Should().Be(SpecimenStatus.RejectedByLab);
        sample.RejectionReason.Should().Be(SpecimenRejectionReason.InsufficientSample);
        pathway.StageAt(1).IsLocked.Should().BeFalse("the stage that took the sample reopens for the repeat");
        Pathways.Verify(p => p.SaveAsync(It.IsAny<PathwayChange>(), Cts.Token), Times.Once);
        var told = Sent.Should().ContainSingle().Subject;
        told.Should().BeEquivalentTo(new
        {
            Kind = NotificationKind.RepeatSampleRequested,
            Title = "Repeat sample requested",
            Message = $"The lab asked for a repeat sample (Histology). The {ConsultationStage} stage is open again for the repeat.",
            UserId = Staff.UserId,
            SubjectId = (Guid?)sample.Id,
        });
        SavedAudit.Should().Contain(e => e.Action == PatientPathwayAuditAction.RepeatSampleRequested)
            .And.Contain(e => e.Action == PatientPathwayAuditAction.SpecimenRejected)
            .And.AllSatisfy(e => e.Details.Should().NotContain("too little"));
    }

    [Test]
    public async Task StepEnteredInError_OnlyTheLatest_ByWhoeverRecordedIt()
    {
        var labCase = GivenCase(LabPathway(), null, ThroughRequiredSteps());
        var latest = labCase.Steps.Last();
        var earlier = labCase.Steps[1];

        var notLatest = () => StepErrors.Handle(new MarkLabStepEnteredInErrorCommand(labCase.Id, earlier.Id, "x", 2, LabMember), Cts.Token);
        await notLatest.Should().ThrowAsync<ConflictException>().WithMessage(LabCaseRules.LatestStepOnlyMessage);

        var notMine = () => StepErrors.Handle(new MarkLabStepEnteredInErrorCommand(labCase.Id, latest.Id, "x", 2, Pathologist), Cts.Token);
        await notMine.Should().ThrowAsync<ForbiddenException>().WithMessage(LabCaseRules.OwnStepOnlyMessage);

        await StepErrors.Handle(new MarkLabStepEnteredInErrorCommand(labCase.Id, latest.Id, "Synthetic: wrong block", 2, LabMember), Cts.Token);
        latest.IsEnteredInError.Should().BeTrue();
        LabCaseRules.Next(labCase)!.TestStep!.Id.Should().Be(latest.TestStepId!.Value);
    }
}

[TestFixture]
public sealed class LabCaseLifecycleHandlersTests : LabCaseTestBase
{
    private AuthoriseLabReportHandler Authorise =>
        new(CaseAccess, LabCases.Object, Pathways.Object, Identity.Object, Notifications.Object, Clock);

    private MarkLabCaseEnteredInErrorHandler CaseErrors => new(CaseAccess, LabCases.Object, Clock);

    private OpenLabCaseHandler Open => new(CaseAccess, LabCases.Object, Repository.Object, Pathways.Object, Boundary.Object, Receipt, Clock);

    private LabCase ReadyToReport(PatientPathway pathway)
    {
        var labCase = GivenCase(pathway, null, ThroughRequiredSteps());
        labCase.Steps.Add(Step(labCase, 5, LabCaseStepKind.Outcome, choice: LabChoice.NeedsMoreWork, outcome: LabOutcome.NoMoreWork));
        return labCase;
    }

    [Test]
    public async Task Authorise_ReportsTheSamples_MakesTheResult_AndPutsTheResultsOnTheLabStage_WithoutFinishingIt()
    {
        var pathway = LabPathway();
        var labCase = ReadyToReport(pathway);
        var sample = labCase.Specimens.Single();
        var stage = pathway.StageAt(2);

        var response = await Authorise.Handle(new AuthoriseLabReportCommand(labCase.Id, " Synthetic results. ", 2, Pathologist), Cts.Token);

        response.Status.Should().Be("Reported");
        response.ReportText.Should().Be("Synthetic results.");
        sample.Status.Should().Be(SpecimenStatus.Reported);
        var result = SavedResults.Should().ContainSingle().Subject;
        result.Should().BeEquivalentTo(new
        {
            LabCaseId = (Guid?)labCase.Id,
            ReportText = "Synthetic results.",
            ResponsibleUserId = sample.RequestingUserId,
            SpecimenId = (Guid?)sample.Id,
            Source = ResultSource.Specimen,
            ReportAttachmentId = Guid.Empty,
        });
        labCase.ResultId.Should().Be(result.Id);
        stage.ResultText.Should().Be("Synthetic results.");
        stage.StartedAtUtc.Should().NotBeNull();
        stage.PickedUpAtUtc.Should().NotBeNull();
        stage.CompletedAtUtc.Should().BeNull("the lab finishes its stage by hand");
        Pathways.Verify(p => p.SaveAsync(It.IsAny<PathwayChange>(), Cts.Token), Times.Once);
        Sent.Should().ContainSingle(n => n.Kind == NotificationKind.ResultReady && n.UserId == sample.RequestingUserId);
        SavedAudit.Should().Contain(e => e.Action == PatientPathwayAuditAction.LabReportAuthorised)
            .And.AllSatisfy(e => e.Details.Should().NotContain("Synthetic results"));
    }

    [Test]
    public async Task Authorise_WithoutThePermission_IsA403_AndBeforeTheStepsAreDone_A409()
    {
        var pathway = LabPathway();
        var notReady = GivenCase(pathway, null, ThroughRequiredSteps());

        var notAllowed = () => Authorise.Handle(new AuthoriseLabReportCommand(notReady.Id, "x", 2, LabMember), Cts.Token);
        await notAllowed.Should().ThrowAsync<ForbiddenException>();

        var early = () => Authorise.Handle(new AuthoriseLabReportCommand(notReady.Id, "x", 2, Pathologist), Cts.Token);
        await early.Should().ThrowAsync<ConflictException>()
            .WithMessage("The case's steps aren't finished: Record the outcome of “Needs more work?” first.");
    }

    [Test]
    public void AuthoriseValidator_NeedsTheResults_AtMost4000()
    {
        var validator = new AuthoriseLabReportValidator();
        validator.Validate(new AuthoriseLabReportCommand(Guid.NewGuid(), " ", 1, Pathologist)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == LabCaseRules.ResultsMissingMessage);
        validator.Validate(new AuthoriseLabReportCommand(Guid.NewGuid(), new string('x', 4001), 1, Pathologist)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == "The results must be at most 4,000 characters.");
    }

    [Test]
    public async Task CaseEnteredInError_ByItsOpener_FreesItsSamples_ElseA403()
    {
        var labCase = GivenCase(LabPathway());
        var sample = labCase.Specimens.Single();

        var other = () => CaseErrors.Handle(new MarkLabCaseEnteredInErrorCommand(labCase.Id, "x", 2, Pathologist), Cts.Token);
        await other.Should().ThrowAsync<ForbiddenException>().WithMessage(LabCaseRules.CaseEnteredInErrorNotAllowedMessage);

        var response = await CaseErrors.Handle(new MarkLabCaseEnteredInErrorCommand(labCase.Id, "Synthetic: wrong case", 2, LabMember), Cts.Token);

        response.Status.Should().Be("EnteredInError");
        sample.LabCaseId.Should().BeNull();
        SavedAudit.Should().ContainSingle(e => e.Action == PatientPathwayAuditAction.LabCaseEnteredInError);
    }

    [Test]
    public async Task Open_AReceivedSampleWithNone_OpensANewCase_WithTheTestChosen()
    {
        var pathway = LabPathway();
        pathway.StageAt(2).OwnerTeamId = null;
        var sample = Given(CreateReceived(pathway));
        sample.LabTestId = null;
        sample.Version = 4;

        var response = await Open.Handle(new OpenLabCaseCommand(sample.Id, Histology.Id, 4, LabMember), Cts.Token);

        response.LabTestName.Should().Be("Histology");
        response.Number.Should().Be("MA26-000123");
        response.StageId.Should().Be(sample.StageId);
        sample.LabTestId.Should().Be(Histology.Id);
        TrackedCases.Should().ContainSingle().Which.Case.Steps.Should().ContainSingle(s => s.Kind == LabCaseStepKind.Received);
    }

    [Test]
    public async Task Open_ALabStagesWaitingCase_TakesTheSample_AndPicksTheStageUp()
    {
        var pathway = LabPathway();
        var waiting = new LabCase
        {
            Id = Guid.NewGuid(), Number = "MA26-000400", PatientId = pathway.PatientId, Patient = pathway.Patient,
            Source = SpecimenSource.Clinic, RequestingTeamId = pathway.TeamId, PathwayId = pathway.Id, StageId = pathway.StageAt(2).Id,
            Status = LabCaseStatus.Open, OpenedAtUtc = Now.AddDays(-1), Version = 1,
        };
        pathway.StageAt(2).LabCases.Add(waiting);
        LabCases.Setup(l => l.GetOpenForStagesAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(pathway.StageAt(2).Id)), It.IsAny<CancellationToken>()))
            .ReturnsAsync([waiting]);
        var sample = Given(CreateReceived(pathway));
        sample.Version = 4;

        var response = await Open.Handle(new OpenLabCaseCommand(sample.Id, Histology.Id, 4, LabMember), Cts.Token);

        response.Id.Should().Be(waiting.Id);
        waiting.LabTestId.Should().Be(Histology.Id);
        waiting.ReceivedAtUtc.Should().Be(sample.ReceivedAtUtc);
        pathway.StageAt(2).PickedUpAtUtc.Should().NotBeNull();
        Pathways.Verify(p => p.SaveAsync(It.IsAny<PathwayChange>(), Cts.Token), Times.Once);
    }

    [Test]
    public async Task Open_IsRefused_ForASampleNotReceived_InACase_OrChangedMeanwhile()
    {
        var pathway = LabPathway();
        var sample = Given(CreateReceived(pathway));
        sample.Version = 4;

        (await FluentActions.Awaiting(() => Open.Handle(new OpenLabCaseCommand(sample.Id, Histology.Id, 3, LabMember), Cts.Token))
            .Should().ThrowAsync<ConflictException>()).WithMessage(OpenLabCaseHandler.SampleChangedMessage);

        sample.Status = SpecimenStatus.Sent;
        (await FluentActions.Awaiting(() => Open.Handle(new OpenLabCaseCommand(sample.Id, Histology.Id, 4, LabMember), Cts.Token))
            .Should().ThrowAsync<ConflictException>()).WithMessage(OpenLabCaseHandler.NotReceivedMessage);

        var archived = MedApp.UnitTests.Common.Constants.SpecimensTestConstants.Bloods();
        archived.IsArchived = true;
        LabCases.Setup(l => l.GetTestAsync(archived.Id, It.IsAny<CancellationToken>())).ReturnsAsync(archived);
        sample.Status = SpecimenStatus.Received;
        (await FluentActions.Awaiting(() => Open.Handle(new OpenLabCaseCommand(sample.Id, archived.Id, 4, LabMember), Cts.Token))
            .Should().ThrowAsync<ValidationException>()).WithMessage($"*{OpenLabCaseHandler.TestMessage}*");
    }

    [Test]
    public async Task Get_RecordsLabCaseViewed_WithTheAccess()
    {
        var labCase = GivenCase(LabPathway());
        var activity = new RecordingActivityLog();

        var response = await new GetLabCaseHandler(CaseAccess, activity, Clock).Handle(new GetLabCaseQuery(labCase.Id, LabMember), Cts.Token);

        response.Number.Should().Be("MA26-000500");
        response.CanRecord.Should().BeTrue();
        response.Samples.Should().ContainSingle();
        var view = activity.SingleViews.Should().ContainSingle().Subject;
        view.Action.Should().Be(ActivityAction.LabCaseViewed);
        view.Details.Should().Contain(labCase.Id.ToString()).And.Contain("Owner");
    }

    [Test]
    public async Task ReceivingASample_OpensItsLabStagesCase_AndPicksTheStageUp()
    {
        var pathway = LabPathway();
        var sample = Given(MedApp.UnitTests.Common.Constants.SpecimensTestConstants.CreateSpecimen(pathway, SpecimenStatus.Sent));
        sample.LabTestId = Histology.Id;

        var response = await new MarkSpecimenReceivedHandler(
                Repository.Object, Pathways.Object, LabCases.Object, Receipt, Boundary.Object, Teams.Object, Identity.Object, QrCodes.Object, Clock)
            .Handle(new MarkSpecimenReceivedCommand(sample.Id, null, null, 3, LabMember), Cts.Token);

        response.Status.Should().Be("Received");
        var opened = TrackedCases.Should().ContainSingle().Subject.Case;
        opened.StageId.Should().Be(pathway.StageAt(2).Id);
        opened.LabTestId.Should().Be(Histology.Id);
        sample.LabCaseId.Should().Be(opened.Id);
        pathway.StageAt(2).PickedUpAtUtc.Should().NotBeNull();
        response.LabCaseNumber.Should().Be(opened.Number);
        SavedAudit.Should().Contain(e => e.Action == PatientPathwayAuditAction.LabCaseOpened)
            .And.Contain(e => e.Action == PatientPathwayAuditAction.SpecimenReceived);
    }
}

[TestFixture]
public sealed class LabCaseSearchTests : LabCaseTestBase
{
    private static SearchLabCasesQuery Query(string? view, string? sort = null) =>
        new(1, 50, view, null, null, null, sort, true, Staff);

    [Test]
    public void Views_FollowTheClockAndNextStep_InMemory()
    {
        var pathway = LabPathway();
        var ready = GivenCase(pathway, null, ThroughRequiredSteps());
        ready.Steps.Add(Step(ready, 5, LabCaseStepKind.Outcome, choice: LabChoice.NeedsMoreWork, outcome: LabOutcome.NoMoreWork));
        var late = GivenCase(LabPathway());
        late.ReceivedAtUtc = Now.AddDays(-20);
        var reported = GivenCase(LabPathway());
        reported.Status = LabCaseStatus.Reported;
        reported.ReportedAtUtc = Now;
        var all = new[] { ready, late, reported };

        List<LabCase> Run(string view)
        {
            var query = Query(view);
            return LabCaseSearch.Arrange(all.AsQueryable().Filter(query), query, Now).ToList();
        }

        Query("Open").NeedsMemory.Should().BeTrue();
        Query("Reported").NeedsMemory.Should().BeFalse();
        Run("Open").Should().Equal(late, ready);
        Run("ToReport").Should().Equal(ready);
        Run("Overdue").Should().Equal(late);
        Run("AtRisk").Should().BeEmpty();
        Run("Reported").Should().Equal(reported);
    }

    [Test]
    public void Validator_RefusesAnUnknownViewOrSource_AndALongSearch()
    {
        var validator = new SearchLabCasesValidator();
        validator.Validate(Query("Lunch")).IsValid.Should().BeFalse();
        validator.Validate(Query(null) with { Source = "Moon" }).IsValid.Should().BeFalse();
        validator.Validate(Query(null) with { Search = new string('x', 201) }).IsValid.Should().BeFalse();
        validator.Validate(Query("toReport") with { Source = "clinic" }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Summary_SaysWhereItCameFrom_WhatsNext_AndItsClock()
    {
        var labCase = GivenCase(LabPathway());
        labCase.RequestingTeam = new MedApp.Domain.Teams.Team { Id = TeamId, Name = "Dermatology" };

        var summary = LabCaseMapper.ToSummary(labCase, Now);

        summary.Should().BeEquivalentTo(new { SourceName = "Dermatology", NextName = "Accessioned", NextKind = "Step", LabTestName = "Histology" });
        summary.Clock!.State.Should().Be(LabClock.OnTime);
    }
}
