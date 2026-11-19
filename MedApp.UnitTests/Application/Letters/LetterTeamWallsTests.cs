using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.Letters.Commands.ApproveLetter;
using MedApp.Application.Letters.Commands.CreateLetter;
using MedApp.Application.Letters.Commands.UpdateLetter;
using MedApp.Application.Letters.Commands.WithdrawLetter;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Queries.GetLetter;
using MedApp.Application.Letters.Queries.SearchLetters;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Letters;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;
using MedApp.Infrastructure.Data;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Moq;
using static MedApp.UnitTests.Common.Constants.LettersTestConstants;

namespace MedApp.UnitTests.Application.Letters;

/// <summary>
/// The team walls on letters (MC5, the light demo's <c>findLetter</c>), through the handlers with the real
/// <see cref="TeamBoundary"/>: a letter follows its pathway, or its own team when it has none; another team's is a 404
/// as if it weren't there (recorded); one the viewer reads but their team doesn't own is a 409 to change (but a
/// withdrawal by someone who corrects records). Writing one needs its pathway or referral to be the writer's team's.
/// Lists and the count filter in SQL; the clinic picker narrows them (MC6).
/// </summary>
[TestFixture]
public sealed class LetterTeamWallsTests : LetterHandlerTestBase
{
    private const string NotOursMessage = "This letter is another team’s: your team can read it, and only its own team changes it.";

    private readonly CancellationTokenSource _cts = new();

    [OneTimeTearDown]
    public void TearDown() => _cts.Dispose();

    private static readonly Guid Lab = Guid.Parse("0000005a-0000-0000-0000-0000000000ab");

    private static PathwayActor In(PathwayActor actor, params Guid[] teams) =>
        actor with { Scope = GrantsTeamBoundary.MemberOf(actor.UserId, teams) };

    /// <summary>A letter on <see cref="LetterHandlerTestBase.Pathway"/>, its team's, the team's grant written with the pathway.</summary>
    private Letter GivenOurs(LetterStatus status = LetterStatus.Draft, PathwayActor? author = null)
    {
        var letter = OnPathway(status, author);
        letter.TeamId = Pathway.TeamId;
        Walls.Grants.Add(PathwayAccess.Owner(Pathway.Id, Pathway.PatientId, Pathway.TeamId, NowUtc));
        return Given(letter);
    }

    private void PassedToTheLab() =>
        Walls.Grants.Add(PathwayAccess.StageOwner(Pathway.Id, Pathway.PatientId, Lab, NowUtc));

    private void ExpectRefusalRecorded(string targetKind, Guid targetId)
    {
        var (action, details) = Activity.Once.Should().ContainSingle().Subject;
        action.Should().Be(ActivityAction.TeamBoundaryRefused);
        var json = JsonDocument.Parse(details).RootElement;
        json.GetProperty("targetKind").GetString().Should().Be(targetKind);
        json.GetProperty("targetId").GetGuid().Should().Be(targetId);
    }

    [Test]
    public async Task Get_AnotherTeamsLetter_IsA404WithAMissingOnesWords_AndIsRecorded()
    {
        var letter = GivenOurs();

        var act = () => Fixture.Create<GetLetterHandler>().Handle(new GetLetterQuery(letter.Id, In(Approver, Guid.NewGuid())), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Letter '{letter.Id}' was not found.");
        ExpectRefusalRecorded("Letter", letter.Id);
        Activity.SingleViews.Should().BeEmpty();
    }

    [Test]
    public async Task Get_ItsTeams_RecordsTheViewWithAccessOwner_AndTheLabsThroughThePathwayWithStageOwner()
    {
        var letter = GivenOurs();
        PassedToTheLab();

        await Fixture.Create<GetLetterHandler>().Handle(new GetLetterQuery(letter.Id, In(Approver, Pathway.TeamId)), _cts.Token);
        await Fixture.Create<GetLetterHandler>().Handle(new GetLetterQuery(letter.Id, In(Sender, Lab)), _cts.Token);

        Activity.SingleViews.Select(v => v.Details).Should().Equal(
            $"{{\"letterId\":\"{letter.Id}\",\"access\":\"Owner\"}}",
            $"{{\"letterId\":\"{letter.Id}\",\"access\":\"StageOwner\"}}");
    }

    [Test]
    public async Task Preview_AnotherTeamsDraft_IsA404()
    {
        var letter = GivenOurs();

        var act = () => Fixture.Create<PreviewLetterHandler>().Handle(new PreviewLetterQuery(letter.Id, In(Author, Guid.NewGuid())), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
        Renderer.Verify(r => r.RenderAsync(It.IsAny<LetterDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Update_BehindTheWall_IsA404_AndNothingIsSaved()
    {
        var letter = GivenOurs();

        var act = () => Fixture.Create<UpdateLetterHandler>().Handle(new UpdateLetterCommand(
            letter.Id, Letterhead, LetterBody, Closing, false, [], letter.Version, In(Author, Guid.NewGuid())), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
        ExpectRefusalRecorded("Letter", letter.Id);
        Letters.Verify(l => l.SaveAsync(It.IsAny<IEnumerable<MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Approve_ByATeamThatOnlyReadsThePathway_IsA409NamingWhoseItIs()
    {
        var letter = GivenOurs(LetterStatus.AwaitingApproval);
        PassedToTheLab();

        var act = () => Fixture.Create<ApproveLetterHandler>().Handle(
            new ApproveLetterCommand(letter.Id, letter.Version, In(Approver, Lab)), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(NotOursMessage);
        letter.Status.Should().Be(LetterStatus.AwaitingApproval);
    }

    [Test]
    public async Task Withdraw_ByAnApproverOfAnotherTeam_IsA409_ButBySomeoneWhoCorrectsRecords_IsACorrection()
    {
        var letter = GivenOurs(LetterStatus.Approved);
        PassedToTheLab();

        var byTheLab = () => Fixture.Create<WithdrawLetterHandler>().Handle(
            new WithdrawLetterCommand(letter.Id, "Synthetic: wrong patient's practice.", letter.Version, In(Approver, Lab)), _cts.Token);
        await byTheLab.Should().ThrowAsync<ConflictException>().WithMessage(NotOursMessage);

        var admin = AdminActor with { Scope = GrantsTeamBoundary.AdminScope(AdminActor.UserId) };
        await Fixture.Create<WithdrawLetterHandler>().Handle(
            new WithdrawLetterCommand(letter.Id, "Synthetic: wrong patient's practice.", letter.Version, admin), _cts.Token);

        letter.Status.Should().Be(LetterStatus.Withdrawn);
    }

    [Test]
    public async Task Create_OnAPathwayBehindTheWall_IsA404_AndOnOneTheirTeamOnlyReads_A409()
    {
        var template = CreateTemplate(teamId: Pathway.TeamId);
        var templates = new Mock<ILetterTemplateRepository>();
        templates.Setup(t => t.GetAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        Fixture.Inject(templates.Object);
        Walls.Grants.Add(PathwayAccess.Owner(Pathway.Id, Pathway.PatientId, Pathway.TeamId, NowUtc));

        var hidden = () => Fixture.Create<CreateLetterHandler>().Handle(
            new CreateLetterCommand(template.Id, Pathway.Id, null, In(Author, Guid.NewGuid())), _cts.Token);
        await hidden.Should().ThrowAsync<NotFoundException>().WithMessage(PathwayRepositoryExtensions.NotFoundMessage(Pathway.Id));
        ExpectRefusalRecorded(TeamWalls.PathwayTarget, Pathway.Id);

        PassedToTheLab();
        var readOnly = () => Fixture.Create<CreateLetterHandler>().Handle(
            new CreateLetterCommand(template.Id, Pathway.Id, null, In(Author, Lab)), _cts.Token);
        await readOnly.Should().ThrowAsync<ConflictException>().WithMessage("This pathway is another team*");
        Added.Should().BeEmpty();

        await Fixture.Create<CreateLetterHandler>().Handle(
            new CreateLetterCommand(template.Id, Pathway.Id, null, In(Author, Pathway.TeamId)), _cts.Token);
        Added.Single().TeamId.Should().Be(Pathway.TeamId);
    }

    [Test]
    public async Task Create_OnAnotherTeamsReferral_IsA404_AndIsRecorded()
    {
        var referral = ReferralsTestConstants.CreateReferral(Patient);
        Referrals.Setup(r => r.GetByIdAsync(referral.Id, It.IsAny<CancellationToken>())).ReturnsAsync(referral);
        var template = CreateTemplate(teamId: referral.TeamId, kind: LetterKind.ReferralResponse);
        var templates = new Mock<ILetterTemplateRepository>();
        templates.Setup(t => t.GetAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        Fixture.Inject(templates.Object);

        var act = () => Fixture.Create<CreateLetterHandler>().Handle(
            new CreateLetterCommand(template.Id, null, referral.Id, In(Author, Guid.NewGuid())), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Referral '{referral.Id}' was not found.");
        ExpectRefusalRecorded("Referral", referral.Id);
        Added.Should().BeEmpty();
    }

    // The list, the count and the clinic picker.

    private static SearchLettersQuery Search(TeamScope scope, Guid? teamId = null) =>
        new(1, 10, "All", null, null, null, Author.UserId, TeamId: teamId, Scope: scope);

    [Test]
    public void Apply_KeepsTheViewersTeamsAndThoseWhosePathwayTheySee_AndThePickerNarrows()
    {
        var ours = OnPathway();
        ours.TeamId = PathwaysTestConstants.TeamId;
        var theirPathway = PathwaysTestConstants.CreatePathway(PatientPathwayStatus.InProgress, patient: Patient);
        var theirsOnAPathwayWeSee = CreateLetter(theirPathway, Practice);
        theirsOnAPathwayWeSee.TeamId = Guid.NewGuid();
        var theirsOnAReferral = new Letter
        {
            Id = Guid.NewGuid(), PatientId = Patient.Id, ReferralId = Guid.NewGuid(), TeamId = Guid.NewGuid(), CreatedAt = CreatedAtUtc
        };
        var grants = new List<PathwayAccess>
        {
            PathwayAccess.StageOwner(theirPathway.Id, Patient.Id, PathwaysTestConstants.TeamId, NowUtc)
        }.AsQueryable();
        var letters = new[] { ours, theirsOnAPathwayWeSee, theirsOnAReferral }.AsQueryable();
        var member = GrantsTeamBoundary.MemberOf(Author.UserId, PathwaysTestConstants.TeamId);

        LetterSearch.Apply(letters, Search(member), grants: grants).Should().BeEquivalentTo([ours, theirsOnAPathwayWeSee]);
        LetterSearch.Apply(letters, Search(member, PathwaysTestConstants.TeamId), grants: grants).Should().Equal(ours);
        LetterSearch.Apply(letters, Search(GrantsTeamBoundary.AdminScope(Author.UserId))).Should().HaveCount(3);
    }

    [Test]
    public void Apply_WithTheWalls_TranslatesToSql()
    {
        using var context = new MedAppDbContext(new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer("Server=walls.invalid;Database=None")
            .Options);

        var sql = LetterSearch.Apply(
                context.Letters, Search(GrantsTeamBoundary.MemberOf(Author.UserId, PathwaysTestConstants.TeamId), PathwaysTestConstants.TeamId),
                context.Patients, context.PathwayAccess)
            .ToQueryString();

        sql.Should().Contain("EXISTS").And.Contain("[PathwayAccess]");
    }

    [Test]
    public async Task Search_PickingATeamThatIsntTheirs_IsA400OnTeamId()
    {
        var act = () => Fixture.Create<SearchLettersHandler>().Handle(
            Search(GrantsTeamBoundary.MemberOf(Author.UserId, PathwaysTestConstants.TeamId), Guid.NewGuid()), _cts.Token);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().PropertyName.Should().Be("TeamId");
        Letters.Verify(l => l.SearchAsync(It.IsAny<SearchLettersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Search_AnAdminsList_RecordsAccessAndTheTeamFilter()
    {
        Letters.Setup(l => l.SearchAsync(It.IsAny<SearchLettersQuery>(), _cts.Token))
            .ReturnsAsync(new PagedResult<LetterRow>([new LetterRow(OnPathway(), "Sam", "Example", Pathway.Title)], 1));

        await Fixture.Create<SearchLettersHandler>().Handle(Search(GrantsTeamBoundary.AdminScope(AdminActor.UserId)), _cts.Token);

        Activity.ViewDetails.Single().Should().Be("{\"access\":\"Admin\",\"teamFilter\":\"all\"}");
    }

    [Test]
    public async Task Count_PassesTheViewersScope()
    {
        var member = GrantsTeamBoundary.MemberOf(Approver.UserId, PathwaysTestConstants.TeamId);
        Letters.Setup(l => l.CountAsync(Approver.UserId, member, _cts.Token)).ReturnsAsync((1, 4));

        var count = await Fixture.Create<CountLettersHandler>().Handle(new CountLettersQuery(Approver.UserId, member), _cts.Token);

        count.ToApprove.Should().Be(1);
        count.ToSend.Should().Be(4);
    }
}
