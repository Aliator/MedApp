using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Results.Common;
using MedApp.Domain.Notifications;
using MedApp.Domain.Results;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.ResultsTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Results.Common;

[TestFixture]
public sealed class ResultWorkflowTests
{
    [Test]
    public void ActionsInOrder_AreEveryActionInTheirStoredOrder()
    {
        ResultWorkflow.ActionsInOrder.Should().Equal(Enum.GetValues<ResultAction>());
    }

    [TestCase(ResultStatus.AwaitingSignOff, true)]
    [TestCase(ResultStatus.Seen, true)]
    [TestCase(ResultStatus.SignedOff, false)]
    [TestCase(ResultStatus.EnteredInError, false)]
    public void IsAwaiting_OnlyBeforeSignOff(ResultStatus status, bool awaiting)
    {
        ResultWorkflow.IsAwaiting(status).Should().Be(awaiting);
        CreateResult(status: status).IsAwaitingSignOff.Should().Be(awaiting);
    }

    [Test]
    public void MayMarkEnteredInError_TheReporterNeedsTheirPermissionStill()
    {
        var result = CreateResult();
        var reporterWithoutPermission = new PathwayActor(SpecimensTestConstants.LabStaff.UserId, OtherUsername);

        ResultWorkflow.MayMarkEnteredInError(result, SpecimensTestConstants.LabStaff, Now).Should().BeTrue();
        ResultWorkflow.MayMarkEnteredInError(result, reporterWithoutPermission, Now).Should().BeFalse();
        ResultWorkflow.MayMarkEnteredInError(result, Clinician, Now).Should().BeFalse();
        ResultWorkflow.MayMarkEnteredInError(result, SpecimensTestConstants.Corrector, Now.AddYears(1)).Should().BeTrue();
    }

    [Test]
    public void AgeDays_IsWholeDaysSinceReported()
    {
        ResultWorkflow.AgeDays(CreateResult(reportedAtUtc: Now.AddDays(-3).AddHours(-2)), Now).Should().Be(3);
        ResultWorkflow.AgeDays(CreateResult(reportedAtUtc: Now.AddHours(1)), Now).Should().Be(0);
    }

    [Test]
    public void Gate_LastStageOrWaitsForResults_RefusesWhileOneAwaits_NamingOnlyDates()
    {
        var awaiting = new[] { CreateResult(), CreateResult(status: ResultStatus.Seen) };

        var why = ResultWorkflow.WhyAwaitingResultsBlockFinish(awaiting, isLastStage: true, waitsForResults: false);

        why.Should().Contain("2 results await sign-off").And.NotContain(awaiting[0].Title);
        ResultWorkflow.WhyAwaitingResultsBlockFinish(awaiting, isLastStage: false, waitsForResults: true).Should().Contain("waits for results");
        ResultWorkflow.WhyAwaitingResultsBlockFinish(awaiting, isLastStage: false, waitsForResults: false).Should().BeNull();
        ResultWorkflow.WhyAwaitingResultsBlockFinish([CreateResult(status: ResultStatus.SignedOff)], true, true).Should().BeNull();
    }

    [Test]
    public void StageGates_WhyResultsAwaiting_UsesTheStagesFlagAndWhetherItsLast()
    {
        var pathway = CreatePathway(Status.InProgress);
        var first = pathway.StageAt(1);

        StageGates.WhyResultsAwaiting(pathway, first, [CreateResult()]).Should().BeNull();
        first.WaitsForResults = true;
        StageGates.WhyResultsAwaiting(pathway, first, [CreateResult()]).Should().NotBeNull();
    }
}

[TestFixture]
public sealed class ResultMapperAndNotificationTests
{
    [Test]
    public void ToResponse_GivesTheFlagsForTheViewer()
    {
        var result = CreateResult();
        var report = ReportOf(result);

        var response = ResultMapper.ToResponse(result, Usernames, report, responsibleCanSignOff: true, Clinician, Now);

        response.Should().BeEquivalentTo(new
        {
            result.Id,
            Source = nameof(ResultSource.Specimen),
            SpecimenNumber = SpecimensTestConstants.Number,
            SpecimenTypeName = SpecimensTestConstants.BiopsyName,
            ReportFileName = report.FileName,
            ResponsibleUsername = Staff.Username,
            ReportedByUsername = OtherUsername,
            NeedsClinician = false,
            IsAwaitingSignOff = true,
            CanMarkSeen = true,
            CanSignOff = true,
            CanReassign = true,
            CanMarkEnteredInError = false,
            ReadOnlyReason = (string?)null,
        });
    }

    [Test]
    public void ToResponse_ResponsibleCantSignOff_NeedsAClinician_AndADeletedFileHasNoName()
    {
        var result = CreateResult();
        var report = ReportOf(result);
        report.DeletedAtUtc = Now;

        var response = ResultMapper.ToResponse(result, Usernames, report, responsibleCanSignOff: false, SpecimensTestConstants.LabStaff, Now);

        response.NeedsClinician.Should().BeTrue();
        response.ReportFileName.Should().BeNull();
        response.CanSignOff.Should().BeFalse("the lab can't sign off");
        response.CanMarkEnteredInError.Should().BeTrue("the lab reported it within 24 hours");
    }

    [Test]
    public void ToSummary_GivesDatesNamesAndStatuses()
    {
        var result = CreateResult(reportedAtUtc: Now.AddDays(-15));

        var summary = ResultMapper.ToSummary(result, Usernames, new HashSet<Guid>(), Now);

        summary.AgeDays.Should().Be(15);
        summary.NeedsClinician.Should().BeTrue();
        summary.ResponsibleUsername.Should().Be(Staff.Username);
        summary.SpecimenNumber.Should().Be(SpecimensTestConstants.Number);
    }

    [Test]
    public void ReadOnlyReason_SignedOffAndEnteredInError()
    {
        ResultMapper.ReadOnlyReason(CreateResult(status: ResultStatus.SignedOff)).Should().Contain("signed off");
        ResultMapper.ReadOnlyReason(CreateResult(status: ResultStatus.EnteredInError)).Should().Contain("entered in error");
    }

    [Test]
    public void Notifications_HaveFixedTitles_NoPatientOrFreeText_AndSkipTheActor()
    {
        var result = CreateResult();

        var ready = ResultNotifications.Ready(result, SpecimensTestConstants.LabStaff, Now)!;
        ready.Title.Should().Be(ResultNotifications.ReadyTitle);
        ready.Message.Should().Be($"{OtherUsername} reported a {SpecimensTestConstants.BiopsyName} sample.");
        ready.Message.Should().NotContain(result.Patient.FirstName).And.NotContain(SpecimensTestConstants.Number);
        Notification.HasFixedTitle(ready.Kind).Should().BeTrue();
        Notification.HasFixedTitle(NotificationKind.ResultReassigned).Should().BeTrue();
        Notification.HasFixedTitle(NotificationKind.ResultSignedOffByOther).Should().BeTrue();

        ResultNotifications.Ready(result, Clinician, Now).Should().BeNull();
        ResultNotifications.Reassigned(result, OtherClinician, Now)!.Title.Should().Be(ResultNotifications.ReassignedTitle);
    }

    [Test]
    public void ResultChange_EveryEntryNamesTheResultAndNeverItsFreeText()
    {
        var result = CreateResult();
        result.Title = ExternalTitle;
        var change = new ResultChange(result, result.Pathway, Clinician, Now);

        change.Record(Staff.Username);
        change.MarkSeen();
        change.SignOff(ResultAction.Other, ActionDetail);

        change.AuditEntries.Should().HaveCount(3);
        change.AuditEntries.Select(e => e.AtUtc).Should().BeInAscendingOrder();
        change.AuditEntries.Should().AllSatisfy(e =>
        {
            e.Details.Should().Contain(result.Id.ToString()).And.NotContain(ExternalTitle).And.NotContain(ActionDetail);
            e.PatientPathwayId.Should().Be(result.PathwayId!.Value);
        });
        result.Version.Should().Be(3);
    }
}
