using FluentAssertions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Lab.Common;
using MedApp.Domain.Lab;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;
using MedApp.Domain.Teams;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.TheLab.Common;

[TestFixture]
public sealed class LabCaseRulesTests : LabCaseTestBase
{
    [Test]
    public void Next_RunsReceivedAccessionedTheChoiceTheRequiredStepsThenNeedsMoreWork()
    {
        var pathway = LabPathway();
        var labCase = GivenCase(pathway);

        LabCaseRules.Next(labCase)!.Should().BeEquivalentTo(new { Kind = LabNextKind.Step, Name = "Accessioned" });

        labCase.Steps.Add(Step(labCase, 1, LabCaseStepKind.Accessioned));
        var adequate = LabCaseRules.Next(labCase)!;
        adequate.Should().BeEquivalentTo(new { Kind = LabNextKind.Choice, Name = "Sample adequate?", Choice = LabChoice.SampleAdequate });
        adequate.Outcomes.Should().Equal(LabOutcome.Adequate, LabOutcome.Insufficient);

        labCase.Steps.Add(Step(labCase, 2, LabCaseStepKind.Outcome, choice: LabChoice.SampleAdequate, outcome: LabOutcome.Adequate));
        LabCaseRules.Next(labCase)!.Name.Should().Be("Grossing");

        foreach (var (step, i) in ThroughRequiredSteps(labCase).Skip(2).Select((s, i) => (s, i)))
            labCase.Steps.Add(step);
        var more = LabCaseRules.Next(labCase)!;
        more.Should().BeEquivalentTo(new { Kind = LabNextKind.Choice, Name = "Needs more work?" });
        more.Outcomes.Should().Equal(LabOutcome.NoMoreWork, LabOutcome.ExtraTests, LabOutcome.SecondOpinion);
    }

    [Test]
    public void Next_AnExtraOutcome_AddsItsOptionalSteps_ThenAsksAgain_AndNoMoreWorkIsTheReport()
    {
        var pathway = LabPathway();
        var labCase = GivenCase(pathway, null, ThroughRequiredSteps());
        labCase.Steps.Add(Step(labCase, 5, LabCaseStepKind.Outcome, choice: LabChoice.NeedsMoreWork, outcome: LabOutcome.SecondOpinion));

        var sent = LabCaseRules.Next(labCase)!;
        sent.TestStep!.Kind.Should().Be(LabStepKind.SentForSecondOpinion);
        labCase.Steps.Add(Step(labCase, 6, LabCaseStepKind.TestStep, testStepId: sent.TestStep.Id));
        LabCaseRules.Next(labCase)!.TestStep!.Kind.Should().Be(LabStepKind.SecondOpinionReturned);
        labCase.Steps.Add(Step(labCase, 7, LabCaseStepKind.TestStep, testStepId: LabCaseRules.Next(labCase)!.TestStep!.Id));
        LabCaseRules.Next(labCase)!.Choice.Should().Be(LabChoice.NeedsMoreWork);

        labCase.Steps.Add(Step(labCase, 8, LabCaseStepKind.Outcome, choice: LabChoice.NeedsMoreWork, outcome: LabOutcome.NoMoreWork));
        LabCaseRules.Next(labCase)!.Kind.Should().Be(LabNextKind.Report);
    }

    [Test]
    public void Next_AStepEnteredInError_IsToDoAgain_AndATestWithoutOptionalSteps_OffersOnlyNoMoreWork()
    {
        var pathway = LabPathway();
        var labCase = GivenCase(pathway, null, ThroughRequiredSteps());
        labCase.Steps.Last().EnteredInErrorAtUtc = Now;
        LabCaseRules.Next(labCase)!.Name.Should().Be("Processing");

        var bloods = MedApp.UnitTests.Common.Constants.SpecimensTestConstants.Bloods();
        var other = GivenCase(LabPathway());
        other.LabTest = bloods;
        other.Steps.Add(Step(other, 1, LabCaseStepKind.Accessioned));
        other.Steps.Add(Step(other, 2, LabCaseStepKind.Outcome, choice: LabChoice.SampleAdequate, outcome: LabOutcome.Adequate));
        other.Steps.Add(Step(other, 3, LabCaseStepKind.TestStep, testStepId: bloods.Steps[0].Id));
        LabCaseRules.Next(other)!.Outcomes.Should().Equal(LabOutcome.NoMoreWork);
    }

    [Test]
    public void Next_NotReceived_IsReceive_AndClosedIsNone()
    {
        var labCase = GivenCase(LabPathway());
        labCase.Steps.Clear();
        LabCaseRules.Next(labCase)!.Kind.Should().Be(LabNextKind.Receive);

        labCase.Status = LabCaseStatus.Reported;
        LabCaseRules.Next(labCase).Should().BeNull();
    }

    [Test]
    public void NotNext_SaysWhatIsAsked()
    {
        LabCaseRules.NotNext(new LabNext(LabNextKind.Choice, "Sample adequate?")).Should().Be("Record the outcome of “Sample adequate?” first.");
        LabCaseRules.NotNext(new LabNext(LabNextKind.Report, "Results and report"))
            .Should().Be("Every step is done: enter the results and authorise the report.");
        LabCaseRules.NotNext(new LabNext(LabNextKind.Step, "Grossing")).Should().Be("The next step is Grossing.");
    }

    [Test]
    public void ReadOnlyReason_AndWhoWorksCases()
    {
        var labCase = GivenCase(LabPathway());

        LabCaseRules.ReadOnlyReason(labCase, LabMember, Team.LabId).Should().BeNull();
        LabCaseRules.ReadOnlyReason(labCase, ClinicStaff, Team.LabId).Should().Be(LabCaseRules.LabWorkMessage);
        labCase.Status = LabCaseStatus.RepeatRequested;
        LabCaseRules.ReadOnlyReason(labCase, LabMember, Team.LabId)
            .Should().Be("This case has been closed with a repeat sample requested, so it can't be changed.");
    }

    [Test]
    public void MayMarkEnteredInError_ItsOpenerWithin24Hours_OrCorrectRecords()
    {
        var labCase = GivenCase(LabPathway());

        LabCaseRules.MayMarkEnteredInError(labCase, LabMember, Team.LabId, Now).Should().BeTrue();
        LabCaseRules.MayMarkEnteredInError(labCase, LabMember, Team.LabId, labCase.OpenedAtUtc.AddHours(25)).Should().BeFalse();
        LabCaseRules.MayMarkEnteredInError(labCase, Pathologist, Team.LabId, Now).Should().BeFalse();
        LabCaseRules.MayMarkEnteredInError(labCase, MedApp.UnitTests.Common.Constants.SpecimensTestConstants.Corrector, Team.LabId, Now)
            .Should().BeTrue();
    }
}

[TestFixture]
public sealed class LabCaseChangeTests : LabCaseTestBase
{
    [Test]
    public void Open_WithSamples_RecordsReceivedOnce_AtVersion1_AndAuditsWithoutFreeText()
    {
        var pathway = LabPathway();
        var sample = CreateReceived(pathway);

        var change = LabCaseChange.Open("MA26-000777", Histology.Id, Histology, pathway.PatientId, Team.LabId, SpecimenSource.Clinic,
            pathway.TeamId, null, null, pathway, pathway.StageAt(2).Id, [sample], LabMember, Now);

        var labCase = change.Case;
        labCase.Version.Should().Be(1);
        labCase.ReceivedAtUtc.Should().Be(sample.ReceivedAtUtc);
        labCase.Steps.Should().ContainSingle(s => s.Kind == LabCaseStepKind.Received);
        sample.LabCaseId.Should().Be(labCase.Id);
        labCase.Specimens.Should().Equal(sample);
        var entry = change.AuditEntries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientPathwayAuditAction.LabCaseOpened);
        entry.PatientPathwayId.Should().Be(pathway.Id);
        entry.TeamId.Should().Be(pathway.TeamId);
        entry.Details.Should().Contain("\"number\":\"MA26-000777\"").And.Contain("\"test\":\"Histology\"").And.Contain("\"source\":\"Clinic\"");
    }

    [Test]
    public void Insufficient_ClosesForARepeat_KeepingWhyOnTheCaseOnly()
    {
        var labCase = GivenCase(LabPathway(), null, ThroughRequiredSteps()[..1]);
        var change = new LabCaseChange(labCase, labCase.Pathway, LabMember, Now);

        change.RecordOutcome(LabChoice.SampleAdequate, LabOutcome.Insufficient, "Synthetic: too little tissue");

        labCase.Status.Should().Be(LabCaseStatus.RepeatRequested);
        labCase.RepeatDetail.Should().Be("Synthetic: too little tissue");
        labCase.Version.Should().Be(3);
        change.AuditEntries.Select(e => e.Action).Should().Equal(PatientPathwayAuditAction.LabStepRecorded, PatientPathwayAuditAction.RepeatSampleRequested);
        change.AuditEntries.Should().AllSatisfy(e => e.Details.Should().NotContain("Synthetic"));
        change.AuditEntries[0].Details.Should().Contain("\"outcome\":\"Insufficient\"");
    }

    [Test]
    public void EnteredInError_FreesItsSamples()
    {
        var labCase = GivenCase(LabPathway());
        var sample = labCase.Specimens.Single();

        new LabCaseChange(labCase, labCase.Pathway, LabMember, Now).MarkEnteredInError("Synthetic: wrong case", labCase.Specimens);

        labCase.Status.Should().Be(LabCaseStatus.EnteredInError);
        sample.LabCaseId.Should().BeNull();
        labCase.Specimens.Should().BeEmpty();
    }
}

[TestFixture]
public sealed class LabStagesAndWallsTests : LabCaseTestBase
{
    [Test]
    public void LabStageAfter_FindsTheNearestLabStage_ByLinksOrInOrder()
    {
        var pathway = LabPathway();

        LabStages.LabStageAfter(pathway, pathway.StageAt(1).Id, Team.LabId).Should().Be(pathway.StageAt(2));
        LabStages.LabStageAfter(pathway, pathway.StageAt(3).Id, Team.LabId).Should().BeNull();
        LabStages.LabStageAfter(pathway, pathway.StageAt(1).Id, null).Should().BeNull();

        pathway.Links.Add(new PatientPathwayStageLink { PatientPathwayId = pathway.Id, FromStageId = pathway.StageAt(1).Id, ToStageId = pathway.StageAt(3).Id });
        LabStages.LabStageAfter(pathway, pathway.StageAt(1).Id, Team.LabId).Should().BeNull("the links say stage 1 leads to stage 3 only");
    }

    [Test]
    public void CaseOf_IsTheOpenOne_ElseTheLatestNotEnteredInError()
    {
        var pathway = LabPathway();
        var stage = pathway.StageAt(2);
        var first = GivenCase(pathway);
        first.Status = LabCaseStatus.RepeatRequested;
        var error = new LabCase { Id = Guid.NewGuid(), Status = LabCaseStatus.EnteredInError, OpenedAtUtc = Now };
        stage.LabCases.Add(error);

        LabStages.CaseOf(stage).Should().Be(first);
        LabStages.OpenCaseOf(stage).Should().BeNull();

        var open = new LabCase { Id = Guid.NewGuid(), Status = LabCaseStatus.Open, OpenedAtUtc = Now.AddDays(-9) };
        stage.LabCases.Add(open);
        LabStages.CaseOf(stage).Should().Be(open);
    }

    [Test]
    public void TestFromSamples_IsAWaitingSampleTakenForTheStage()
    {
        var pathway = LabPathway();
        var taken = MedApp.UnitTests.Common.Constants.SpecimensTestConstants.CreateSpecimen(pathway);
        taken.LabTestId = Histology.Id;

        LabStages.TestFromSamples(pathway, pathway.StageAt(2), [taken], Team.LabId).Should().Be(Histology.Id);
        taken.Status = SpecimenStatus.Received;
        LabStages.TestFromSamples(pathway, pathway.StageAt(2), [taken], Team.LabId).Should().BeNull();
    }

    [Test]
    public void Walls_TheLabAndAdminsSeeAll_AClinicItsOwn_NeverAHospitals()
    {
        var pathway = LabPathway();
        var mine = GivenCase(pathway);
        var hospital = new LabCase { Id = Guid.NewGuid(), Source = SpecimenSource.Hospital, Number = "MA26-000600" };
        var others = new LabCase { Id = Guid.NewGuid(), Source = SpecimenSource.Clinic, RequestingTeamId = Guid.NewGuid(), PathwayId = Guid.NewGuid() };
        var cases = new[] { mine, hospital, others }.AsQueryable();
        var noGrants = Array.Empty<PathwayAccess>().AsQueryable();
        var admin = new TeamScope(Guid.NewGuid(), [], seesAllTeams: true);

        cases.VisibleCases(LabMember.Teams, noGrants, Team.LabId).Should().HaveCount(3);
        cases.VisibleCases(admin, noGrants, Team.LabId).Should().HaveCount(3);
        cases.VisibleCases(ClinicStaff.Teams, noGrants, Team.LabId).Should().Equal(mine);

        var samples = new[] { mine.Specimens.Single(), new Specimen { Id = Guid.NewGuid(), Source = SpecimenSource.Hospital, TeamId = TeamId } }.AsQueryable();
        samples.VisibleSpecimens(ClinicStaff.Teams, noGrants, Team.LabId).Should().ContainSingle(s => s.Source == SpecimenSource.Clinic);
        samples.VisibleSpecimens(LabMember.Teams, noGrants, Team.LabId).Should().HaveCount(2);
    }

    [Test]
    public void WhyLabCaseOpen_RefusesAManualFinish_WhileTheStagesCaseIsOpen()
    {
        var pathway = LabPathway();
        var labCase = GivenCase(pathway);

        MedApp.Application.Pathways.Common.StageGates.WhyLabCaseOpen(pathway.StageAt(2), Team.LabId)
            .Should().Be("Lab case MA26-000500 is still open: authorise its report first, then finish this stage.");
        MedApp.Application.Pathways.Common.StageGates.WhyLabCaseOpen(pathway.StageAt(3), Team.LabId).Should().BeNull();

        labCase.Status = LabCaseStatus.Reported;
        MedApp.Application.Pathways.Common.StageGates.WhyLabCaseOpen(pathway.StageAt(2), Team.LabId).Should().BeNull();
    }

    [Test]
    public void PathwayResponse_NamesTheLabStagesCase()
    {
        var pathway = LabPathway();
        var labCase = GivenCase(pathway);

        var response = MedApp.Application.Pathways.Common.PathwayMapper.ToResponse(pathway, Usernames, Now);

        response.Stages.Single(s => s.Id == pathway.StageAt(2).Id)
            .Should().BeEquivalentTo(new { LabCaseId = (Guid?)labCase.Id, LabCaseNumber = "MA26-000500", LabCaseStatus = "Open" });
        response.Stages.Single(s => s.Id == pathway.StageAt(1).Id).LabCaseId.Should().BeNull();
    }
}
