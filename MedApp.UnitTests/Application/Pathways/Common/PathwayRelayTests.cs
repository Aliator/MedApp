using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.Common;

/// <summary>
/// The relay (MC7): stages owned by another team (the lab), handed over when reached, picked up, worked by their team,
/// the worklist views, the handover notification, and the stage reopened for a repeat sample (MC22).
/// </summary>
[TestFixture]
public sealed class PathwayRelayTests
{
    private static readonly Guid Lab = Guid.Parse("0000005a-0000-0000-0000-0000000000bb");
    private static readonly Team Clinic = new() { Id = TeamId, Name = "Dermatology" };
    private static readonly Team LabTeam = new() { Id = Lab, Name = "Lab", Kind = TeamKind.Lab };

    private static TeamScope ScopeOf(Guid team) => new(Staff.UserId, [team], false);

    private static readonly IReadOnlySet<Permission> Works = new HashSet<Permission> { Permission.WorkPathways, Permission.UnlockStages };

    /// <summary>A chain of three in the clinic: the second stage is the lab's.</summary>
    private static PatientPathway Relay(int finishedStages = 0, Status status = Status.InProgress)
    {
        var pathway = CreatePathway(status, finishedStages: finishedStages);
        pathway.TeamId = TeamId;
        pathway.Team = Clinic;
        pathway.StageAt(2).OwnerTeamId = Lab;
        pathway.StageAt(2).OwnerTeam = LabTeam;
        return pathway;
    }

    [Test]
    public void ReachNext_TheLabsStageReached_HandsItOverOnceWithAGrantAndAnEntryNamingTheTeams()
    {
        var pathway = Relay(finishedStages: 1);
        var change = new PathwayChange(pathway, Staff, Now);

        change.ReachNext(TeamId);
        change.ReachNext(TeamId);

        var stage = pathway.StageAt(2);
        stage.ReachedAtUtc.Should().Be(Now);
        stage.HandedOverFromTeamId.Should().Be(TeamId);
        change.ReachedStages.Should().Equal(stage);
        change.Handovers.Should().ContainSingle().Which.Should().Be(new PathwayHandover(stage, TeamId, Lab));
        change.NewGrants.Should().ContainSingle().Which.Should().Match<PathwayAccess>(g =>
            g.TeamId == Lab && g.Reason == PathwayAccessReason.StageOwner && g.PathwayId == pathway.Id);
        var entry = change.AuditEntries.Should().ContainSingle(e => e.Action == PatientPathwayAuditAction.StageHandedOver).Subject;
        entry.StageId.Should().Be(stage.Id);
        entry.Details.Should().Contain("\"from\":\"Dermatology\"").And.Contain("\"to\":\"Lab\"");
    }

    [Test]
    public void ReachNext_TheSameTeamsStage_IsReachedWithoutAHandover()
    {
        var pathway = Relay();
        var change = new PathwayChange(pathway, Staff, Now);

        change.ReachNext(TeamId);

        pathway.StageAt(1).ReachedAtUtc.Should().Be(Now);
        pathway.StageAt(1).HandedOverFromTeamId.Should().BeNull();
        change.Handovers.Should().BeEmpty();
        change.AuditEntries.Should().BeEmpty();
    }

    [Test]
    public void StartStage_AHandedOverStage_PicksItUp_AndPickUpIsOnce()
    {
        var pathway = Relay(finishedStages: 1);
        pathway.StageAt(2).HandedOverFromTeamId = TeamId;
        var change = new PathwayChange(pathway, Staff, Now);

        change.StartStage(pathway.StageAt(2));

        pathway.StageAt(2).PickedUpAtUtc.Should().Be(Now);
        change.PickUp(pathway.StageAt(2)).Should().BeFalse();
        change.AuditEntries.Select(e => e.Action).Should().Equal(PatientPathwayAuditAction.StageStarted, PatientPathwayAuditAction.StagePickedUp);
    }

    [Test]
    public void ToResponse_ForTheClinic_TheLabsCurrentStageIsWithAnotherTeam_AndForTheLabItsToWork()
    {
        var pathway = Relay(finishedStages: 1);
        pathway.StageAt(2).HandedOverFromTeamId = TeamId;
        pathway.StageAt(2).ReachedAtUtc = Now.AddHours(-1);

        var clinic = PathwayMapper.ToResponse(pathway, Usernames, Now, viewerPermissions: Works, viewerScope: ScopeOf(TeamId));
        var lab = PathwayMapper.ToResponse(pathway, Usernames, Now, viewerPermissions: Works, viewerScope: ScopeOf(Lab));

        clinic.Stages[1].Should().Match<MedApp.Contracts.Pathways.PatientPathways.Responses.PatientPathwayStageResponse>(s =>
            s.IsWithAnotherTeam && !s.CanEdit && !s.CanPickUp && s.OwnerTeamName == "Lab" && s.HandedOverFromTeamName == "Dermatology");
        clinic.CanEditDetails.Should().BeTrue();
        clinic.ReadOnlyReason.Should().BeNull();

        lab.Stages[1].Should().Match<MedApp.Contracts.Pathways.PatientPathways.Responses.PatientPathwayStageResponse>(s =>
            !s.IsWithAnotherTeam && s.CanEdit && s.CanPickUp);
        lab.CanWorkOnStages.Should().BeTrue();
        lab.CanAddNotes.Should().BeTrue();
        lab.ReadOnlyReason.Should().BeNull();
        lab.CanEditDetails.Should().BeFalse();
        lab.Stages[1].CanUnlock.Should().BeFalse();
        lab.AllowedStatusChanges.Should().BeEmpty();
    }

    [Test]
    public void ToResponse_TheLabAfterHandingBack_ReadsItOnly()
    {
        var pathway = Relay(finishedStages: 2);

        var lab = PathwayMapper.ToResponse(pathway, Usernames, Now, viewerPermissions: Works, viewerScope: ScopeOf(Lab));

        lab.CanWorkOnStages.Should().BeFalse();
        lab.CanAddNotes.Should().BeFalse();
        lab.ReadOnlyReason.Should().Be(TeamWalls.NotYourTeams("Dermatology"));
    }

    [Test]
    public void ToSummary_NamesEveryCurrentStageAndWhoHasIt_AndWhetherItAwaitsPickUp()
    {
        var pathway = Relay(finishedStages: 1);
        pathway.StageAt(2).HandedOverFromTeamId = TeamId;

        var summary = PathwayMapper.ToSummary(pathway, Usernames, Now);

        summary.CurrentStageName.Should().Be(PathwayStage);
        summary.CurrentStageTeamName.Should().Be("Lab");
        summary.AwaitingPickUp.Should().BeTrue();
        PathwayMapper.AndList(["A", "B", "C"]).Should().Be("A, B and C");
        PathwayMapper.AndList(["A", "B"]).Should().Be("A and B");
    }

    // The worklist views ---------------------------------------------------------------------------------------------

    private static IQueryable<PatientPathway> InView(string view, Guid team, params PatientPathway[] pathways) =>
        pathways.AsQueryable().Filter(CreateSearchQuery() with { View = view, Scope = ScopeOf(team), LabTeamId = Lab }, Now);

    [Test]
    public void Views_WhileTheLabHasItsStage()
    {
        var withLab = Relay(finishedStages: 1);
        withLab.StageAt(2).HandedOverFromTeamId = TeamId;
        var clinicsTurn = Relay();

        InView(PathwaySearch.ViewOurTurn, Lab, withLab, clinicsTurn).Should().Equal(withLab);
        InView(PathwaySearch.ViewNewForUs, Lab, withLab, clinicsTurn).Should().Equal(withLab);
        InView(PathwaySearch.ViewAwaitingLab, TeamId, withLab, clinicsTurn).Should().Equal(withLab);
        InView(PathwaySearch.ViewOurTurn, TeamId, withLab, clinicsTurn).Should().Equal(clinicsTurn);
        InView(PathwaySearch.ViewBackFromLab, TeamId, withLab, clinicsTurn).Should().BeEmpty();
    }

    [Test]
    public void Views_BackFromTheLab_UntilPickedUp_AndNeverOnHold()
    {
        var back = Relay(finishedStages: 2);
        back.StageAt(3).HandedOverFromTeamId = Lab;
        var onHold = Relay(finishedStages: 2, status: Status.Blocked);
        onHold.StageAt(3).HandedOverFromTeamId = Lab;

        InView(PathwaySearch.ViewBackFromLab, TeamId, back, onHold).Should().Equal(back);
        InView(PathwaySearch.ViewNewForUs, TeamId, back, onHold).Should().Equal(back);

        back.StageAt(3).PickedUpAtUtc = Now;
        InView(PathwaySearch.ViewBackFromLab, TeamId, back).Should().BeEmpty();
        InView(PathwaySearch.ViewOurTurn, TeamId, back).Should().Equal(back);
    }

    [Test]
    public void Filter_TheClinicPicker_KeepsThatTeamsPathways()
    {
        var mine = Relay();
        var other = Relay();
        other.TeamId = Guid.NewGuid();

        new[] { mine, other }.AsQueryable().Filter(CreateSearchQuery() with { TeamId = TeamId }, Now).Should().Equal(mine);
    }

    [Test]
    public void AtStage_MatchesAnyCurrentStage_OnEachRoute()
    {
        var pathway = Relay(finishedStages: 1);

        new[] { pathway }.AsQueryable().AtStage([PathwayDefinitionId]).Should().Equal(pathway);
        new[] { pathway }.AsQueryable().AtStage([ConsultationDefinitionId]).Should().BeEmpty();
    }

    [Test]
    public void Validate_AViewOrATeam_AsTheDemoRefusesThem()
    {
        var validator = new SearchPatientPathwaysValidator();
        var mine = CreateSearchQuery() with { Scope = ScopeOf(TeamId) };

        validator.Validate(mine with { View = "NewForUs", TeamId = TeamId }).IsValid.Should().BeTrue();
        validator.Validate(mine with { View = "Everything" }).Errors.Should().ContainSingle(e => e.ErrorMessage == PathwaySearch.ViewMessage);
        validator.Validate(mine with { TeamId = Lab }).Errors.Should().ContainSingle(e => e.ErrorMessage == PathwaySearch.TeamNotYoursMessage);
        validator.Validate(CreateSearchQuery() with { TeamId = Lab }).IsValid.Should().BeTrue("the system (and admins) may pick any team");
    }

    // The handover notification -------------------------------------------------------------------------------------

    [Test]
    public async Task NotifyAsync_TellsTheReceivingTeamsMembersWhoWorkPathways_ButNotWhoPassedIt()
    {
        var pathway = Relay(finishedStages: 1);
        var change = new PathwayChange(pathway, Staff, Now);
        change.ReachNext(TeamId);
        var worker = Guid.NewGuid();
        var reader = Guid.NewGuid();
        var teams = new Mock<ITeamRepository>();
        teams.Setup(t => t.GetMemberIdsAsync(Lab, It.IsAny<CancellationToken>())).ReturnsAsync([worker, reader, Staff.UserId]);
        var holders = new Mock<IPermissionHolders>();
        holders.Setup(h => h.WhoHoldsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), Permission.WorkPathways, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { worker, Staff.UserId });
        var sent = new List<Notification>();
        var sender = new Mock<INotificationSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification n, CancellationToken _) => sent.Add(n)).Returns(Task.CompletedTask);

        await new PathwayHandovers(teams.Object, holders.Object, sender.Object).NotifyAsync(change, CancellationToken.None);

        var notification = sent.Should().ContainSingle().Subject;
        notification.UserId.Should().Be(worker);
        notification.Kind.Should().Be(NotificationKind.PathwayHandedOver);
        notification.Title.Should().Be("Pathway handed over");
        notification.Message.Should().Be($"{Staff.Username} passed a pathway to your team.");
        notification.PathwayId.Should().Be(pathway.Id);
    }

    // The lab's results, and a stage reopened for a repeat sample ---------------------------------------------------

    [Test]
    public void PutLabResults_OnTheLabsCurrentStage_EntersThemStartsItAndPicksItUp_WithoutFinishingIt()
    {
        var pathway = Relay(finishedStages: 1);
        pathway.StageAt(2).HandedOverFromTeamId = TeamId;
        var change = new PathwayChange(pathway, Staff, Now);

        change.PutLabResults(pathway.StageAt(2), "Synthetic results.", Now.AddMinutes(-5));

        var stage = pathway.StageAt(2);
        stage.ResultText.Should().Be("Synthetic results.");
        stage.ResultEnteredByUserId.Should().Be(Staff.UserId);
        stage.StartedAtUtc.Should().Be(Now);
        stage.PickedUpAtUtc.Should().Be(Now);
        stage.IsCompleted.Should().BeFalse();
        change.AuditEntries.Should().NotContain(e => e.Details.Contains("Synthetic results."));
    }

    [Test]
    public void ReopenForRepeatSample_TheFinishedStageThatTookIt_IsUnlocked_ButNotTheLabsOrAnOpenOne()
    {
        var pathway = Relay(finishedStages: 2, status: Status.InReview);
        var change = new PathwayChange(pathway, Staff, Now);

        change.ReopenForRepeatSample(pathway.StageAt(2), Lab).Should().BeNull();
        change.ReopenForRepeatSample(pathway.StageAt(3), Lab).Should().BeNull();
        change.ReopenForRepeatSample(pathway.StageAt(1), Lab).Should().Be(ConsultationStage);

        pathway.StageAt(1).IsLocked.Should().BeFalse();
        pathway.Status.Should().Be(Status.InProgress);
        change.AuditEntries.Should().Contain(e => e.Action == PatientPathwayAuditAction.StageUnlocked && e.Details.Contains("repeatSample"));
    }

    [Test]
    public void ToResponse_ForATeamAskedAConsult_SaysWhereToReply()
    {
        var pathway = Relay();
        var other = Guid.NewGuid();

        var asked = PathwayMapper.ToResponse(
            pathway, Usernames, Now, viewerPermissions: Works, viewerScope: ScopeOf(other), teamName: "Dermatology",
            viewerAccess: TeamAccessKind.Consult);
        var reading = PathwayMapper.ToResponse(
            pathway, Usernames, Now, viewerPermissions: Works, viewerScope: ScopeOf(other), teamName: "Dermatology",
            viewerAccess: TeamAccessKind.StageOwner);

        asked.ReadOnlyReason.Should().Be(TeamWalls.ConsultReadOnly("Dermatology"))
            .And.Be("Your team was asked a consult by Dermatology: you can read this pathway until they close it, and reply in Consults.");
        asked.CanAddNotes.Should().BeFalse();
        asked.CanEditDetails.Should().BeFalse();
        reading.ReadOnlyReason.Should().Be(TeamWalls.NotYourTeams("Dermatology"));
    }
}
