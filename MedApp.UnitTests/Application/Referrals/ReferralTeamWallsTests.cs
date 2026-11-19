using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Commands.EditReferral;
using MedApp.Application.Referrals.Commands.RejectReferral;
using MedApp.Application.Referrals.Commands.WithdrawReferral;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Queries.GetReferralById;
using MedApp.Application.Referrals.Queries.SearchReferrals;
using MedApp.Domain.Activity;
using MedApp.Domain.Referrals;
using MedApp.Domain.Teams;
using MedApp.Infrastructure.Data;
using MedApp.UnitTests.Application.Referrals.Commands;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Moq;
using static MedApp.UnitTests.Common.Constants.ReferralsTestConstants;

namespace MedApp.UnitTests.Application.Referrals;

/// <summary>
/// The team walls on referrals (MC5, the light demo's <c>findReferral</c> and <c>referralToChange</c>), through the
/// handlers with the real <see cref="TeamBoundary"/>: a referral is its current team's, or its pathway's once accepted;
/// another team's is a 404 as if it weren't there (recorded); one the viewer reads but their team doesn't own (an
/// admin's, the lab's through the pathway) is a 409 to change. Lists and the count filter in SQL; the clinic picker
/// narrows them (MC6).
/// </summary>
[TestFixture]
public sealed class ReferralTeamWallsTests : TriageCommandTestBase
{
    private const string NotOursMessage =
        "This referral is " + TeamsTestConstants.TeamName + "’s: your team can read it, and only its own team changes it.";

    private static PathwayActor In(PathwayActor actor, params Guid[] teams) =>
        actor with { Scope = GrantsTeamBoundary.MemberOf(actor.UserId, teams) };

    private static PathwayActor AsAdmin(PathwayActor actor) =>
        actor with { Scope = GrantsTeamBoundary.AdminScope(actor.UserId) };

    private Referral GivenReadable(Referral referral)
    {
        Repository.Setup(r => r.GetByIdAsync(referral.Id, It.IsAny<CancellationToken>())).ReturnsAsync(referral);
        return referral;
    }

    private void ExpectRefusalRecordedFor(Referral referral)
    {
        var (action, details) = Activity.Once.Should().ContainSingle().Subject;
        action.Should().Be(ActivityAction.TeamBoundaryRefused);
        var json = JsonDocument.Parse(details).RootElement;
        json.GetProperty("targetKind").GetString().Should().Be("Referral");
        json.GetProperty("targetId").GetGuid().Should().Be(referral.Id);
    }

    private GetReferralByIdHandler GetHandler => new(Repository.Object, Boundary, Activity, Clock);

    [Test]
    public async Task Get_AnotherTeamsReferral_IsA404WithAMissingOnesWords_AndIsRecorded()
    {
        var referral = GivenReadable(GivenReferral());

        var act = () => GetHandler.Handle(new GetReferralByIdQuery(referral.Id, In(Registrar, Guid.NewGuid())), Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Referral '{referral.Id}' was not found.");
        ExpectRefusalRecordedFor(referral);
        Activity.SingleViews.Should().BeEmpty();
    }

    [Test]
    public async Task Get_ItsOwnTeams_RecordsTheViewWithAccessOwner()
    {
        var referral = GivenReadable(GivenReferral());

        await GetHandler.Handle(new GetReferralByIdQuery(referral.Id, In(Registrar, Team.Id)), Cts.Token);

        Activity.SingleViews.Single().Details.Should().Be($"{{\"referralId\":\"{referral.Id}\",\"access\":\"Owner\"}}");
    }

    [Test]
    public async Task Get_AcceptedIntoAPathwayTheViewersTeamSees_IsSeenByThatReason()
    {
        var referral = GivenReadable(GivenReferral(ReferralStatus.Accepted));
        referral.PathwayId = Guid.NewGuid();
        var lab = Guid.NewGuid();
        Walls.Grants.Add(PathwayAccess.StageOwner(referral.PathwayId.Value, referral.PatientId, lab, NowUtc));

        await GetHandler.Handle(new GetReferralByIdQuery(referral.Id, In(Registrar, lab)), Cts.Token);

        Activity.SingleViews.Single().Details.Should().Contain("\"access\":\"StageOwner\"");
        Activity.Once.Should().BeEmpty();
    }

    [Test]
    public async Task Get_AnAdmin_SeesEveryTeams_WithAccessAdmin()
    {
        var referral = GivenReadable(GivenReferral());

        await GetHandler.Handle(new GetReferralByIdQuery(referral.Id, AsAdmin(Viewer)), Cts.Token);

        Activity.SingleViews.Single().Details.Should().Contain("\"access\":\"Admin\"");
    }

    [Test]
    public async Task Edit_AnotherTeamsReferral_IsA404_AndNothingIsSaved()
    {
        var referral = GivenReferral();
        var handler = new EditReferralHandler(Repository.Object, Teams.Object, Practices.Object, Boundary, Clock);

        var act = () => handler.Handle(new EditReferralCommand(
            referral.Id, Team.Id, referral.ReceivedOn, Practice, Clinician, OdsCode, "High", Reason, 3,
            In(Registrar, Guid.NewGuid())), Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
        ExpectRefusalRecordedFor(referral);
        Saved.Should().BeEmpty();
    }

    [Test]
    public async Task Withdraw_SeenButAnotherTeams_IsA409NamingWhoseItIs()
    {
        var referral = GivenReferral();
        var handler = new WithdrawReferralHandler(Repository.Object, Boundary, Clock);

        var act = () => handler.Handle(new WithdrawReferralCommand(referral.Id, Detail, 3, AsAdmin(Registrar)), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(NotOursMessage);
        Saved.Should().BeEmpty();
        referral.Status.Should().Be(ReferralStatus.AwaitingTriage);
    }

    [Test]
    public async Task Withdraw_ByItsOwnTeam_ClosesIt()
    {
        var referral = GivenReferral();
        var handler = new WithdrawReferralHandler(Repository.Object, Boundary, Clock);

        await handler.Handle(new WithdrawReferralCommand(referral.Id, Detail, 3, In(Registrar, Team.Id)), Cts.Token);

        referral.Status.Should().Be(ReferralStatus.Withdrawn);
    }

    [Test]
    public async Task Triage_AnotherTeamsReferral_IsA404BeforeTheMembershipRule()
    {
        var referral = GivenReferral();
        var handler = new RejectReferralHandler(Repository.Object, Notifications.Object, Boundary, Clock);

        var act = () => handler.Handle(
            new RejectReferralCommand(referral.Id, "NotOfferedHere", null, 3, In(Triager, Guid.NewGuid())), Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
        ExpectRefusalRecordedFor(referral);
        Repository.Verify(r => r.IsTeamMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The list, the count and the clinic picker.

    private static SearchReferralsQuery Search(TeamScope scope, Guid? teamId = null) =>
        new(1, 10, null, true, null, null, false, null, null, Registrar.UserId, TeamId: teamId, Scope: scope);

    [Test]
    public void Filter_KeepsTheViewersTeamsAndThoseWhosePathwayTheySee_AndThePickerNarrows()
    {
        var other = TeamsTestConstants.CreateTeam(TeamsTestConstants.OtherTeamName);
        var ours = CreateReferral(Patient, Team);
        var theirs = CreateReferral(Patient, other);
        var theirsOnOurPathway = CreateReferral(Patient, other, ReferralStatus.Accepted);
        theirsOnOurPathway.PathwayId = Guid.NewGuid();
        var grants = new List<PathwayAccess>
        {
            PathwayAccess.StageOwner(theirsOnOurPathway.PathwayId.Value, Patient.Id, Team.Id, NowUtc)
        }.AsQueryable();
        var referrals = new[] { ours, theirs, theirsOnOurPathway }.AsQueryable();
        var member = GrantsTeamBoundary.MemberOf(Registrar.UserId, Team.Id);

        referrals.Filter(Search(member), null, Today, grants).Should().BeEquivalentTo([ours, theirsOnOurPathway]);
        referrals.Filter(Search(member, Team.Id), null, Today, grants).Should().Equal(ours);
        referrals.Filter(Search(GrantsTeamBoundary.AdminScope(Registrar.UserId)), null, Today).Should().HaveCount(3);
        referrals.Visible(GrantsTeamBoundary.MemberOf(Registrar.UserId), grants).Should().BeEmpty("someone in no team sees none");
    }

    [Test]
    public void Filter_WithTheWalls_TranslatesToSql()
    {
        using var context = new MedAppDbContext(new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer("Server=walls.invalid;Database=None")
            .Options);

        var sql = context.Referrals
            .Filter(Search(GrantsTeamBoundary.MemberOf(Registrar.UserId, Team.Id), Team.Id), null, Today, context.PathwayAccess)
            .ToQueryString();

        sql.Should().Contain("EXISTS").And.Contain("[TeamId]");
    }

    [Test]
    public async Task Search_PickingATeamThatIsntTheirs_IsA400OnTeamId()
    {
        var handler = new SearchReferralsHandler(Repository.Object, Teams.Object, Activity, Clock);

        var act = () => handler.Handle(Search(GrantsTeamBoundary.MemberOf(Registrar.UserId, Team.Id), Guid.NewGuid()), Cts.Token);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().PropertyName.Should().Be("TeamId");
        Repository.Verify(r => r.SearchAsync(
            It.IsAny<SearchReferralsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Search_AnAdminsList_RecordsAccessAndTheTeamFilter()
    {
        var picked = Guid.NewGuid();
        Repository.Setup(r => r.SearchAsync(It.IsAny<SearchReferralsQuery>(), null, Today, Cts.Token))
            .ReturnsAsync(new PagedResult<ReferralRow>([new(CreateReferral(Patient, Team), false)], 1));
        var handler = new SearchReferralsHandler(Repository.Object, Teams.Object, Activity, Clock);

        await handler.Handle(Search(GrantsTeamBoundary.AdminScope(Registrar.UserId), picked), Cts.Token);

        Activity.ViewDetails.Single().Should().Be($"{{\"access\":\"Admin\",\"teamFilter\":\"{picked}\"}}");
    }
}
