using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Teams.Common;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.ReferralsTestConstants;

namespace MedApp.UnitTests.Application.Referrals.Common;

/// <summary>Triage's shared rules: who may triage, what the second triager is told, the history and the notification.</summary>
[TestFixture]
public sealed class TriageCommonTests
{
    [Test]
    public void WhyCantTriage_InOrder_ClosedPermissionMembershipThenTheRecord()
    {
        var referral = CreateReferral();

        ReferralWorkflow.WhyCantTriage(CreateReferral(status: ReferralStatus.Accepted), Triager, true).Should().BeNull();
        ReferralWorkflow.WhyCantTriage(referral, Registrar, true).Should().Be(ReferralWorkflow.NoTriagePermissionMessage);
        ReferralWorkflow.WhyCantTriage(referral, Triager, false)
            .Should().Be(ReferralWorkflow.NotInTeamMessage(TeamsTestConstants.TeamName));
        ReferralWorkflow.WhyCantTriage(referral, Triager, true).Should().BeNull();

        referral.Patient.AnonymisedAtUtc = NowUtc;
        ReferralWorkflow.WhyCantTriage(referral, Triager, true).Should().NotBeNull();
    }

    [Test]
    public void EnsureCanTriage_IsForbiddenBeforeItsAConflict()
    {
        var closed = CreateReferral(status: ReferralStatus.Withdrawn);

        var outsider = () => ReferralWorkflow.EnsureCanTriage(closed, Triager, isMember: false, closed.Version);
        var member = () => ReferralWorkflow.EnsureCanTriage(closed, Triager, isMember: true, closed.Version);

        outsider.Should().Throw<ForbiddenException>();
        member.Should().Throw<ConflictException>();
    }

    [TestCase(ReferralStatus.Accepted, "a.synthetic accepted it into a pathway on 18 November 2026")]
    [TestCase(ReferralStatus.Rejected, "a.synthetic rejected it on 18 November 2026")]
    [TestCase(ReferralStatus.RedirectedElsewhere, "a.synthetic redirected it elsewhere on 18 November 2026")]
    [TestCase(ReferralStatus.Withdrawn, "a.synthetic withdrew it on 18 November 2026")]
    [TestCase(ReferralStatus.EnteredInError, "a.synthetic marked it entered in error on 18 November 2026")]
    public void AlreadyTriagedMessage_NamesWhoHowAndWhen(ReferralStatus status, string expected)
    {
        var referral = CreateReferral(status: status);
        referral.DecidedByUsername = "a.synthetic";
        referral.DecidedAtUtc = NowUtc;

        ReferralWorkflow.AlreadyTriagedMessage(referral).Should().Contain(expected);
    }

    [Test]
    public void AlreadyTriagedMessage_WithNobodyRecorded_SaysSomeone()
    {
        var referral = CreateReferral(status: ReferralStatus.Accepted);

        ReferralWorkflow.AlreadyTriagedMessage(referral).Should().Contain("Someone accepted it into a pathway.");
    }

    [Test]
    public void Accept_RecordsTheOutcomeTemplatePrioritiesAndPathway()
    {
        var referral = CreateReferral(version: 2);
        var pathwayId = Guid.NewGuid();
        var change = new ReferralChange(referral, Triager, NowUtc);

        change.Accept(pathwayId, "Synthetic template", PatientPathwayPriority.Low);

        referral.Status.Should().Be(ReferralStatus.Accepted);
        referral.PathwayId.Should().Be(pathwayId);
        referral.Version.Should().Be(3);
        referral.LastUpdated.Should().Be(NowUtc);
        var entry = change.Events.Single();
        entry.PathwayId.Should().Be(pathwayId);
        entry.UserId.Should().Be(Triager.UserId);
        entry.Details.Should().Contain("\"priority\":\"Low\"").And.Contain("\"requestedPriority\":\"High\"")
            .And.Contain("Synthetic template").And.NotContain(Reason).And.NotContain(Practice);
    }

    [Test]
    public void RedirectToTeam_KeepsItOpenFromTheSameDate()
    {
        var referral = CreateReferral();
        var urology = TeamsTestConstants.CreateTeam(TeamsTestConstants.OtherTeamName);
        var change = new ReferralChange(referral, Triager, NowUtc);

        change.RedirectToTeam(urology, Detail);

        referral.Status.Should().Be(ReferralStatus.AwaitingTriage);
        referral.TeamId.Should().Be(urology.Id);
        referral.RedirectDetail.Should().Be(Detail);
        referral.ReceivedOn.Should().Be(ReceivedOn);
        referral.DecidedAtUtc.Should().BeNull();
        change.Events.Single().Action.Should().Be(ReferralEventAction.TeamChanged);
    }

    [Test]
    public void Triaged_ToTheRegistrar_WithAFixedTitleAndNoPatientOrFreeText()
    {
        var referral = CreateReferral();
        referral.Reason = Canary;
        referral.DecisionDetail = Canary;
        new ReferralChange(referral, Triager, NowUtc).Reject(ReferralRejectionReason.Other, Canary);

        var notification = ReferralNotifications.Triaged(referral, Triager, NowUtc)!;

        notification.UserId.Should().Be(Registrar.UserId);
        notification.Kind.Should().Be(NotificationKind.ReferralTriaged);
        notification.Title.Should().Be("Referral triaged");
        notification.Message.Should().Be($"c.synthetic rejected a {TeamsTestConstants.TeamName} referral.");
        notification.ReferralId.Should().Be(referral.Id);
        notification.PathwayId.Should().BeNull();
        notification.CreatedAtUtc.Should().Be(NowUtc);
        notification.Message.Should().NotContain(Canary).And.NotContain(referral.Patient.LastName);
        Notification.HasFixedTitle(NotificationKind.ReferralTriaged).Should().BeTrue();
        Notification.IsMonitoring(NotificationKind.ReferralTriaged).Should().BeFalse();
    }

    [Test]
    public void Triaged_ByTheRegistrar_IsNobodys() =>
        ReferralNotifications.Triaged(CreateReferral(), Registrar, NowUtc).Should().BeNull();

    [TestCase(ReferralStatus.Accepted, "Urology", null, "k.synthetic accepted a Urology referral.")]
    [TestCase(ReferralStatus.Accepted, "Endocrinology", null, "k.synthetic accepted an Endocrinology referral.")]
    [TestCase(ReferralStatus.RedirectedElsewhere, "Audiology", null, "k.synthetic redirected an Audiology referral elsewhere.")]
    [TestCase(ReferralStatus.AwaitingTriage, "Urology", "Orthopaedics", "k.synthetic redirected an Orthopaedics referral to Urology.")]
    [TestCase(ReferralStatus.AwaitingTriage, "Urology", null, "k.synthetic triaged a Urology referral.")]
    public void Message_ByOutcome(ReferralStatus status, string team, string? previous, string expected)
    {
        var referral = CreateReferral(team: TeamsTestConstants.CreateTeam(team), status: status);

        ReferralNotifications.Message(referral, "k.synthetic", previous).Should().Be(expected);
    }

    [Test]
    public void NotificationMapper_GivesTheReferral()
    {
        var referralId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = Guid.NewGuid(), Kind = NotificationKind.ReferralTriaged, Title = "Referral triaged", Message = "m", ReferralId = referralId
        };

        NotificationMapper.ToResponse(notification).ReferralId.Should().Be(referralId);
    }

    [Test]
    public void PathwayCreation_FromAReferral_RecordsItsIdOnTheCreatedEntry()
    {
        var template = PathwaysTestConstants.CreateTemplate(
            PathwaysTestConstants.TemplateName, false, PathwaysTestConstants.CreateDefinition(PathwaysTestConstants.PathwayStage));
        var pathway = PathwayCreation.FromTemplate(template, Guid.NewGuid(), null, Today, PatientPathwayPriority.High, NowUtc);
        var referralId = Guid.NewGuid();

        var fromReferral = new PathwayChange(pathway, Triager, NowUtc);
        PathwayCreation.Record(fromReferral, template.Name, Today, referralId: referralId);
        var direct = new PathwayChange(pathway, Triager, NowUtc);
        PathwayCreation.Record(direct, template.Name, Today);

        fromReferral.AuditEntries.Single().Details.Should().Contain($"\"referralId\":\"{referralId}\"");
        direct.AuditEntries.Single().Details.Should().NotContain("referralId");
    }

    [Test]
    public void TeamMapper_GivesHowManyMembersCanTriage() =>
        TeamMapper.ToResponse(TeamsTestConstants.CreateTeam(), new TeamUsage(1, 2, 3, 4, 1, TriagerCount: 2))
            .TriagerCount.Should().Be(2);
}
