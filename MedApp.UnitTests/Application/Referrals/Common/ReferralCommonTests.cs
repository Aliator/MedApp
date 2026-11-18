using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.ReferralsTestConstants;

namespace MedApp.UnitTests.Application.Referrals.Common;

[TestFixture]
public sealed class ReferralChangeTests
{
    [Test]
    public void Register_RecordsTheTeamAndPriorityAndStartsAtVersionOne()
    {
        var referral = CreateReferral(version: 0);
        var change = new ReferralChange(referral, Registrar, NowUtc);

        change.Register(referral.Team);

        referral.Version.Should().Be(1);
        var entry = change.Events.Single();
        entry.Action.Should().Be(ReferralEventAction.Registered);
        entry.UserId.Should().Be(Registrar.UserId);
        entry.Username.Should().Be(Registrar.Username);
        entry.AtUtc.Should().Be(NowUtc);
        entry.PatientId.Should().Be(referral.PatientId);
        entry.Details.Should().Contain(referral.Team.Name).And.Contain("High").And.Contain("2026-11-10");
        entry.Details.Should().NotContain(Reason).And.NotContain(Practice).And.NotContain(Clinician);
    }

    [Test]
    public void EditDetails_NamesFreeTextFieldsWithoutValuesAndGivesStructuredOnes()
    {
        var referral = CreateReferral(version: 4);
        var newTeam = TeamsTestConstants.CreateTeam(TeamsTestConstants.OtherTeamName);
        var change = new ReferralChange(referral, Registrar, NowUtc);
        var details = new ReferralDetails(
            newTeam.Id, ReceivedOn.AddDays(-1), OtherPractice, null, null, PatientPathwayPriority.Urgent, Canary);

        var changed = change.EditDetails(details, newTeam, referral.Team);

        changed.Should().BeTrue();
        referral.Version.Should().Be(5);
        referral.TeamId.Should().Be(newTeam.Id);
        referral.Reason.Should().Be(Canary);
        referral.ReferrerOdsCode.Should().BeNull();
        change.Events.Select(e => e.Action).Should().Equal(ReferralEventAction.DetailsEdited, ReferralEventAction.TargetChanged);
        var parsed = ReferralEventDetails.Parse(change.Events[0].Details);
        parsed.ChangedFields.Should().BeEquivalentTo(
            "specialty", "receivedOn", "requestedPriority", "referrerPractice", "referrerClinician", "referrerOdsCode", "reason");
        parsed.TeamName.Should().Be(newTeam.Name);
        parsed.PreviousTeamName.Should().Be(TeamsTestConstants.TeamName);
        parsed.Changes["requestedPriority"].Should().Be(("High", "Urgent"));
        change.Events.Should().AllSatisfy(e => e.Details.Should().NotContain(Canary).And.NotContain(OtherPractice).And.NotContain(Practice));

        // The corrected team and priority re-read the clock's target, counting from the corrected date received.
        referral.TargetDays.Should().Be(newTeam.TargetDaysUrgent);
        referral.WaitStartOn.Should().Be(ReceivedOn.AddDays(-1));
        referral.TargetOn.Should().Be(ReceivedOn.AddDays(-1 + newTeam.TargetDaysUrgent));
        var target = ReferralEventDetails.Parse(change.Events[1].Details);
        target.PreviousTargetDays.Should().Be(28);
        target.TargetDays.Should().Be(14);
    }

    [Test]
    public void EditDetails_NothingChanged_RecordsNothing()
    {
        var referral = CreateReferral(version: 2);
        var change = new ReferralChange(referral, Registrar, NowUtc);
        var same = new ReferralDetails(
            referral.TeamId, referral.ReceivedOn, Practice, Clinician, OdsCode, PatientPathwayPriority.High, Reason);

        change.EditDetails(same, referral.Team, referral.Team).Should().BeFalse();

        change.HasChanges.Should().BeFalse();
        referral.Version.Should().Be(2);
    }

    [TestCase(true, ReferralStatus.Withdrawn, ReferralEventAction.Withdrawn)]
    [TestCase(false, ReferralStatus.EnteredInError, ReferralEventAction.EnteredInError)]
    public void Close_SetsTheOutcomeAndKeepsTheDetailOnlyOnTheReferral(bool withdraw, ReferralStatus status, ReferralEventAction action)
    {
        var referral = CreateReferral();
        var change = new ReferralChange(referral, Registrar, NowUtc);

        if (withdraw)
            change.Withdraw(Canary);
        else
            change.MarkEnteredInError(Canary);

        referral.Status.Should().Be(status);
        referral.DecisionDetail.Should().Be(Canary);
        referral.DecidedAtUtc.Should().Be(NowUtc);
        referral.DecidedByUserId.Should().Be(Registrar.UserId);
        referral.DecidedByUsername.Should().Be(Registrar.Username);
        referral.Version.Should().Be(2);
        change.Events.Select(e => e.Action).Should().Equal(action, ReferralEventAction.ClockStopped);
        referral.StoppedOn.Should().Be(Today);
        var entry = change.Events[0];
        entry.Details.Should().NotContain(Canary);
        ReferralEventDetails.Parse(entry.Details).ToStatus.Should().Be(status.ToString());
    }
}

[TestFixture]
public sealed class ReferralWorkflowTests
{
    [Test]
    public void AwaitingTriageAndInformationRequested_AreOpen()
    {
        ReferralWorkflow.OpenStatuses.Should().Equal(ReferralStatus.AwaitingTriage, ReferralStatus.InformationRequested);
        ReferralWorkflow.ClosedStatuses.Should().NotContain(ReferralWorkflow.OpenStatuses)
            .And.HaveCount(Enum.GetValues<ReferralStatus>().Length - 2);
        ReferralWorkflow.IsOpen(ReferralStatus.InformationRequested).Should().BeTrue();
        ReferralWorkflow.Describe(ReferralStatus.InformationRequested).Should().Be("waiting for more information");
    }

    [Test]
    public void StoredNumbers_NeverChange()
    {
        ((int)ReferralStatus.EnteredInError).Should().Be(5);
        ((int)ReferralRejectionReason.Other).Should().Be(4);
        ((int)ReferralEventAction.Withdrawn).Should().Be(6);
        ((int)ReferralEventAction.TargetChanged).Should().Be(11);
        ((int)MedApp.Domain.Activity.ActivityAction.ReferralViewed).Should().Be(43);
    }

    [Test]
    public void WhyCantEdit_ClosedRestrictedOrAnonymised()
    {
        ReferralWorkflow.WhyCantEdit(CreateReferral()).Should().BeNull();
        ReferralWorkflow.WhyCantEdit(CreateReferral(status: ReferralStatus.Withdrawn)).Should().Contain("withdrawn");

        var restricted = CreateReferral();
        restricted.Patient.RestrictedAtUtc = NowUtc;
        ReferralWorkflow.WhyCantEdit(restricted).Should().Be(PatientRules.RestrictedMessage);
    }

    [Test]
    public void WhyCantClose_AllowsARestrictedRecordButNotAnAnonymisedOne()
    {
        var restricted = CreateReferral();
        restricted.Patient.RestrictedAtUtc = NowUtc;
        ReferralWorkflow.WhyCantClose(restricted).Should().BeNull();

        var anonymised = CreateReferral();
        anonymised.Patient.AnonymisedAtUtc = NowUtc;
        ReferralWorkflow.WhyCantClose(anonymised).Should().Be(PatientRules.AnonymisedMessage);

        ReferralWorkflow.WhyCantClose(CreateReferral(status: ReferralStatus.EnteredInError)).Should().Contain("entered in error");
    }

    [Test]
    public void WhyFilesCantChange_OnlyEnteredInError()
    {
        foreach (var status in Enum.GetValues<ReferralStatus>())
            ReferralWorkflow.WhyFilesCantChange(CreateReferral(status: status))
                .Should().Be(status == ReferralStatus.EnteredInError ? ReferralWorkflow.EnteredInErrorFilesMessage : null);
    }

    [Test]
    public void EnsureVersion_AnotherVersion_IsAConflict()
    {
        var referral = CreateReferral(version: 3);

        FluentActions.Invoking(() => ReferralWorkflow.EnsureVersion(referral, 3)).Should().NotThrow();
        FluentActions.Invoking(() => ReferralWorkflow.EnsureVersion(referral, 2))
            .Should().Throw<ConflictException>().WithMessage(ReferralWorkflow.ChangedMeanwhileMessage);
    }

    [Test]
    public void PossibleDuplicateMessage_SaysWhenEachWasReceivedButNotWhatItSaid()
    {
        var first = CreateReferral();
        var second = CreateReferral();
        second.ReceivedOn = new DateOnly(2026, 11, 3);

        var message = ReferralWorkflow.PossibleDuplicateMessage("Dermatology", [first, second]);

        message.Should().Contain("2 open Dermatology referrals").And.Contain("3 November 2026").And.Contain("10 November 2026")
            .And.NotContain(Reason).And.NotContain(Practice);
        message.IndexOf("3 November", StringComparison.Ordinal).Should().BeLessThan(message.IndexOf("10 November", StringComparison.Ordinal));
    }

    [Test]
    public void Describe_EveryStatusInWords()
    {
        foreach (var status in Enum.GetValues<ReferralStatus>())
            ReferralWorkflow.Describe(status).Should().NotBe(status.ToString());
    }
}

[TestFixture]
public sealed class OdsCodesTests
{
    [TestCase(null, true)]
    [TestCase("", true)]
    [TestCase("  ", true)]
    [TestCase("z99999", true)]
    [TestCase("Z9 9999", true)]
    [TestCase("ab", false)]
    [TestCase("ABCDEFGHIJK", false)]
    [TestCase("Z9999-", false)]
    [TestCase("Ž99999", false)]
    public void IsValidOrEmpty(string? code, bool valid) => OdsCodes.IsValidOrEmpty(code).Should().Be(valid);

    [Test]
    public void Normalise_UpperCaseWithoutSpacesOrNone()
    {
        OdsCodes.Normalise(" z9 9999 ").Should().Be("Z99999");
        OdsCodes.Normalise("  ").Should().BeNull();
    }
}
