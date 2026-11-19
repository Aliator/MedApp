using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;

namespace MedApp.UnitTests.Application.Common.Teams;

/// <summary>
/// The team walls' rules (<see cref="TeamAccess"/>), each expression run in memory, as the leak tests' unit half
/// (decision 205 of <c>docs/multi-clinic-plan.md</c>): another team's pathway is never in a list, the lab sees one once
/// passed it, admins see all, and only the owner team changes one.
/// </summary>
[TestFixture]
public sealed class TeamAccessTests
{
    private static readonly Guid Dermatology = Guid.Parse("0000005a-0000-0000-0000-0000000000d1");
    private static readonly Guid Gynaecology = Guid.Parse("0000005a-0000-0000-0000-0000000000d2");
    private static readonly Guid Lab = Team.LabId;
    private static readonly DateTime Now = new(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc);

    private static readonly TeamScope DermatologyStaff = new(Guid.NewGuid(), [Dermatology], seesAllTeams: false);
    private static readonly TeamScope GynaecologyStaff = new(Guid.NewGuid(), [Gynaecology], seesAllTeams: false);
    private static readonly TeamScope LabStaff = new(Guid.NewGuid(), [Lab], seesAllTeams: false);
    private static readonly TeamScope Admin = TeamScope.For(Guid.NewGuid(), [], Permissions.Admin);

    private static PatientPathway Pathway(Guid teamId) => new() { Id = Guid.NewGuid(), PatientId = Guid.NewGuid(), TeamId = teamId };

    private static List<PatientPathway> Visible(TeamScope scope, IEnumerable<PatientPathway> pathways, IEnumerable<PathwayAccess> grants) =>
        pathways.AsQueryable().Where(TeamAccess.Pathways(scope, grants.AsQueryable())).ToList();

    [Test]
    public void Pathways_OwnTeam_Seen_AnotherTeams_Never()
    {
        var mine = Pathway(Dermatology);
        var theirs = Pathway(Gynaecology);
        PathwayAccess[] grants =
        [
            PathwayAccess.Owner(mine.Id, mine.PatientId, Dermatology, Now),
            PathwayAccess.Owner(theirs.Id, theirs.PatientId, Gynaecology, Now),
        ];

        Visible(DermatologyStaff, [mine, theirs], grants).Should().Equal(mine);
        Visible(GynaecologyStaff, [mine, theirs], grants).Should().Equal(theirs);
    }

    [Test]
    public void Pathways_NoGrant_IsNotSeenEvenByItsOwnTeam()
    {
        // Grants are the only way in: a pathway whose grant is missing is behind every wall but the admins'.
        var orphan = Pathway(Dermatology);

        Visible(DermatologyStaff, [orphan], []).Should().BeEmpty();
        Visible(Admin, [orphan], []).Should().Equal(orphan);
    }

    [Test]
    public void Pathways_TheLab_SeesOnlyThosePassedToIt_AndKeepsThem()
    {
        var passed = Pathway(Dermatology);
        var notPassed = Pathway(Dermatology);
        PathwayAccess[] grants =
        [
            PathwayAccess.Owner(passed.Id, passed.PatientId, Dermatology, Now),
            PathwayAccess.Owner(notPassed.Id, notPassed.PatientId, Dermatology, Now),
            PathwayAccess.StageOwner(passed.Id, passed.PatientId, Lab, Now),
        ];

        Visible(LabStaff, [passed, notPassed], grants).Should().Equal(passed);
        TeamAccess.Strongest(grants.Where(g => g.PathwayId == passed.Id), LabStaff).Should().Be(TeamAccessKind.StageOwner);
        TeamAccess.CanChange(passed, LabStaff).Should().BeFalse("the lab reads a pathway it was passed; its team changes it");
    }

    [Test]
    public void Pathways_AnEndedGrant_NoLongerCounts()
    {
        var pathway = Pathway(Dermatology);
        var consult = new PathwayAccess
        {
            Id = Guid.NewGuid(), PathwayId = pathway.Id, PatientId = pathway.PatientId, TeamId = Gynaecology,
            Reason = PathwayAccessReason.Consult, StartedAtUtc = Now, EndedAtUtc = Now.AddDays(1),
        };

        Visible(GynaecologyStaff, [pathway], [consult]).Should().BeEmpty();
    }

    [Test]
    public void Pathways_AGrantToThePerson_LetsOnlyThemSeeIt()
    {
        var pathway = Pathway(Dermatology);
        var attendee = new TeamScope(Guid.NewGuid(), [Gynaecology], seesAllTeams: false);
        var mdt = new PathwayAccess
        {
            Id = Guid.NewGuid(), PathwayId = pathway.Id, PatientId = pathway.PatientId, UserId = attendee.UserId,
            Reason = PathwayAccessReason.Mdt, StartedAtUtc = Now,
        };

        Visible(attendee, [pathway], [mdt]).Should().Equal(pathway);
        Visible(GynaecologyStaff, [pathway], [mdt]).Should().BeEmpty();
        TeamAccess.Strongest([mdt], attendee).Should().Be(TeamAccessKind.Mdt);
    }

    [Test]
    public void Pathways_AdminsAndTheSystem_SeeEveryTeams()
    {
        var pathways = new[] { Pathway(Dermatology), Pathway(Gynaecology) };

        Visible(Admin, pathways, []).Should().Equal(pathways);
        Visible(TeamScope.System, pathways, []).Should().Equal(pathways);
        TeamAccess.Strongest([], Admin).Should().Be(TeamAccessKind.Admin);
        TeamAccess.Strongest([], TeamScope.System).Should().Be(TeamAccessKind.System);
    }

    [Test]
    public void Strongest_OwnerBeatsTheRest_AndNoneIsNull()
    {
        var pathway = Pathway(Dermatology);
        var both = new TeamScope(Guid.NewGuid(), [Dermatology, Lab], seesAllTeams: false);
        PathwayAccess[] grants =
        [
            PathwayAccess.StageOwner(pathway.Id, pathway.PatientId, Lab, Now),
            PathwayAccess.Owner(pathway.Id, pathway.PatientId, Dermatology, Now),
        ];

        TeamAccess.Strongest(grants, both).Should().Be(TeamAccessKind.Owner);
        TeamAccess.Strongest(grants, GynaecologyStaff).Should().BeNull();
        TeamAccess.CanSee(grants, GynaecologyStaff).Should().BeFalse();
        TeamAccess.CanSee(grants, LabStaff).Should().BeTrue();
    }

    [Test]
    public void CanChange_OnlyTheOwnerTeam_OrTheSystem_NeverAnAdmin()
    {
        var pathway = Pathway(Dermatology);

        TeamAccess.CanChange(pathway, DermatologyStaff).Should().BeTrue();
        TeamAccess.CanChange(pathway, GynaecologyStaff).Should().BeFalse();
        TeamAccess.CanChange(pathway, Admin).Should().BeFalse("admins are read only");
        TeamAccess.CanChange(pathway, TeamScope.System).Should().BeTrue();
    }

    [Test]
    public void ByPathway_AFileOfItsTeamOrOnAPathwaySeen_IsSeen()
    {
        var seen = Pathway(Gynaecology);
        var hidden = Pathway(Gynaecology);
        PathwayAccess[] grants = [PathwayAccess.StageOwner(seen.Id, seen.PatientId, Lab, Now)];
        Attachment[] files =
        [
            new() { Id = Guid.NewGuid(), TeamId = Lab },
            new() { Id = Guid.NewGuid(), TeamId = Gynaecology, PathwayId = seen.Id },
            new() { Id = Guid.NewGuid(), TeamId = Gynaecology, PathwayId = hidden.Id },
            new() { Id = Guid.NewGuid(), TeamId = Gynaecology },
        ];

        var rule = TeamAccess.ByPathway<Attachment>(LabStaff, grants.AsQueryable(), a => a.PathwayId, a => a.TeamId);

        files.AsQueryable().Where(rule).Should().Equal(files[0], files[1]);
        files.AsQueryable().Where(TeamAccess.ByPathway<Attachment>(Admin, grants.AsQueryable(), a => a.PathwayId, a => a.TeamId))
            .Should().HaveCount(4);
    }

    [Test]
    public void ByTeam_OnlyTheViewersTeams()
    {
        Attachment[] files = [new() { Id = Guid.NewGuid(), TeamId = Dermatology }, new() { Id = Guid.NewGuid(), TeamId = Gynaecology }];

        files.AsQueryable().Where(TeamAccess.ByTeam<Attachment>(DermatologyStaff, a => a.TeamId)).Should().Equal(files[0]);
        files.AsQueryable().Where(TeamAccess.ByTeam<Attachment>(Admin, a => a.TeamId)).Should().Equal(files);
        TeamAccess.CanSeeTeam(Gynaecology, DermatologyStaff).Should().BeFalse();
        TeamAccess.CanChangeTeam(Gynaecology, Admin).Should().BeFalse();
    }

    [Test]
    public void Scope_FromPermissions_OnlyViewAllTeamsSeesEveryTeam_AndARequestIsNeverTheSystem()
    {
        var staff = TeamScope.For(Guid.NewGuid(), [Dermatology], StandardRoles.Clinician.Permissions);
        var admin = TeamScope.For(Guid.NewGuid(), [], Permissions.Admin);

        staff.SeesAllTeams.Should().BeFalse();
        staff.IsSystem.Should().BeFalse();
        admin.SeesAllTeams.Should().BeTrue();
        admin.IsSystem.Should().BeFalse();
        TeamScope.System.IsSystem.Should().BeTrue();
        TeamScope.System.SeesAllTeams.Should().BeTrue();
    }

    [Test]
    public void Scope_ComparesTeamsByContent_AndPicksTheSameDefaultTeamEachTime()
    {
        var user = Guid.NewGuid();

        new TeamScope(user, [Dermatology, Gynaecology], false).Should().Be(new TeamScope(user, [Gynaecology, Dermatology], false));
        new TeamScope(user, [Gynaecology, Dermatology], false).DefaultTeamId.Should().Be(Dermatology);
        new TeamScope(user, [], false).DefaultTeamId.Should().BeNull();
    }
}
