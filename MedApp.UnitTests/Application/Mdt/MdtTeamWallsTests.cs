using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Mdt.Commands.DeferMdtItem;
using MedApp.Application.Mdt.Commands.ListForMdt;
using MedApp.Application.Mdt.Commands.RecordMdtOutcome;
using MedApp.Application.Mdt.Commands.WithdrawMdtItem;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Queries.GetPathwayMdtItems;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Mdt;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Application.Mdt.Commands;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.MdtTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Mdt;

/// <summary>
/// The team walls on the MDT list (MC5; the light demo's <c>mdt.ts</c>): an item belongs to its MDT's team and follows
/// its pathway. Lists show only what the viewer may see; an item behind the wall is a 404, recorded; only the pathway's
/// own team lists it, only the item's own team moves or withdraws it (a 409 for anyone else who sees it), and whoever
/// sees it may record the outcome.
/// </summary>
[TestFixture]
public sealed class MdtTeamWallsTests : MdtCommandTestBase
{
    private static readonly Guid Dermatology = MedApp.UnitTests.Common.Constants.MdtTestConstants.Team.Id;
    private static readonly Guid LabTeam = MedApp.Domain.Teams.Team.LabId;
    private static readonly Guid Gynaecology = Guid.Parse("0000005d-0000-0000-0000-0000000000d2");
    private const string DermatologyName = "Synthetic dermatology";

    [SetUp]
    public void SetUpWalls() => Walls.TeamNames[Dermatology] = DermatologyName;

    private static PathwayActor In(PathwayActor actor, params Guid[] teams) =>
        actor with { Scope = TeamWallsTestBoundary.StaffIn(actor.UserId, teams) };

    /// <summary>A Gynaecology pathway (its owner's grant written) listed on Dermatology's MDT.</summary>
    private MdtItem GynaecologyCaseOnDermatologysMdt()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        pathway.TeamId = Gynaecology;
        Walls.Grants.Add(PathwayAccess.Owner(pathway.Id, pathway.PatientId, Gynaecology, Now));
        var item = CreateItem(CreateMdt(Now.AddHours(-1)), pathway: pathway);
        Given(item);
        return item;
    }

    [Test]
    public async Task Withdraw_AnItemBehindTheWall_IsA404AsIfItWerentThere_AndIsRecorded()
    {
        var item = GynaecologyCaseOnDermatologysMdt();

        var act = () => new WithdrawMdtItemHandler(Repository.Object, Walls.Boundary, Clock)
            .Handle(new WithdrawMdtItemCommand(item.Id, 1, In(Nurse, LabTeam)), Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage(MdtWalls.NotFoundMessage(item.Id));
        Walls.ShouldHaveRefused(MdtWalls.ItemTarget, item.Id);
        item.Status.Should().Be(MdtItemStatus.Listed);
    }

    [Test]
    public async Task WithdrawAndDefer_ThePathwaysTeamOnAnotherTeamsMdt_IsA409_OnlyTheMdtsTeamMovesIt()
    {
        var item = GynaecologyCaseOnDermatologysMdt();
        var later = CreateMdt(Now.AddDays(7));
        Given(later);
        var gynaecologyNurse = In(Nurse, Gynaecology);
        var refusal = TeamWalls.RecordNotYours(MdtWalls.ItemWhat, DermatologyName);

        await FluentActions.Awaiting(() => new WithdrawMdtItemHandler(Repository.Object, Walls.Boundary, Clock)
                .Handle(new WithdrawMdtItemCommand(item.Id, 1, gynaecologyNurse), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(refusal);
        await FluentActions.Awaiting(() => new DeferMdtItemHandler(Repository.Object, Walls.Boundary, Teams.Object, Clock)
                .Handle(new DeferMdtItemCommand(item.Id, later.Id, 1, gynaecologyNurse), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(refusal);

        item.Status.Should().Be(MdtItemStatus.Listed);
        Audit.Should().BeEmpty();
        MdtRules.CanWithdraw(item, gynaecologyNurse).Should().BeFalse();
        MdtRules.CanDefer(item, gynaecologyNurse).Should().BeFalse();
    }

    [Test]
    public async Task Withdraw_TheMdtsOwnTeam_WithdrawsIt()
    {
        var item = GynaecologyCaseOnDermatologysMdt();
        var dermatologyNurse = In(Nurse, Dermatology);
        MdtRules.CanWithdraw(item, dermatologyNurse).Should().BeTrue();

        await new WithdrawMdtItemHandler(Repository.Object, Walls.Boundary, Clock)
            .Handle(new WithdrawMdtItemCommand(item.Id, 1, dermatologyNurse), Token);

        item.Status.Should().Be(MdtItemStatus.Withdrawn);
    }

    [Test]
    public async Task RecordOutcome_AnyoneWhoSeesIt_MayRecordIt()
    {
        var item = GynaecologyCaseOnDermatologysMdt();
        var handler = new RecordMdtOutcomeHandler(
            Repository.Object, Walls.Boundary, new IdentityStub().Object, new Mock<MedApp.Application.Notifications.Common.INotificationSender>().Object, Clock);

        await handler.Handle(new RecordMdtOutcomeCommand(item.Id, Outcome, [Staff.UserId], 1, In(Clinician, Gynaecology)), Token);

        item.Status.Should().Be(MdtItemStatus.Discussed);
    }

    [Test]
    public async Task RecordOutcome_BehindTheWall_IsA404()
    {
        var item = GynaecologyCaseOnDermatologysMdt();
        var handler = new RecordMdtOutcomeHandler(
            Repository.Object, Walls.Boundary, new IdentityStub().Object, new Mock<MedApp.Application.Notifications.Common.INotificationSender>().Object, Clock);

        await FluentActions.Awaiting(() => handler.Handle(
                new RecordMdtOutcomeCommand(item.Id, Outcome, [Staff.UserId], 1, In(Clinician, LabTeam)), Token))
            .Should().ThrowAsync<NotFoundException>();
        item.Status.Should().Be(MdtItemStatus.Listed);
    }

    [Test]
    public async Task List_AnotherTeamsPathway_IsA404_OrA409WhereTheViewerSeesIt()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        pathway.TeamId = Gynaecology;
        Walls.Grants.Add(PathwayAccess.Owner(pathway.Id, pathway.PatientId, Gynaecology, Now));
        var pathways = new Mock<IPatientPathwayRepository>();
        pathways.Setup(p => p.GetByIdAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);
        var meeting = CreateMdt(Now.AddDays(2));
        Given(meeting);
        var handler = new ListForMdtHandler(Repository.Object, pathways.Object, Walls.Boundary, Teams.Object, Clock);

        await FluentActions.Awaiting(() => handler.Handle(new ListForMdtCommand(pathway.Id, meeting.Id, Question, In(Nurse, Dermatology)), Token))
            .Should().ThrowAsync<NotFoundException>().WithMessage(PathwayRepositoryExtensions.NotFoundMessage(pathway.Id));
        Walls.Grants.Add(PathwayAccess.StageOwner(pathway.Id, pathway.PatientId, LabTeam, Now));
        await FluentActions.Awaiting(() => handler.Handle(new ListForMdtCommand(pathway.Id, meeting.Id, Question, In(Nurse, LabTeam)), Token))
            .Should().ThrowAsync<ConflictException>();

        Repository.Verify(r => r.AddAsync(It.IsAny<MdtItem>(), It.IsAny<PatientPathwayAuditEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task PathwayItems_AnotherTeamsPathway_IsA404()
    {
        var item = GynaecologyCaseOnDermatologysMdt();
        var pathways = new Mock<IPatientPathwayRepository>();
        pathways.Setup(p => p.GetByIdAsync(item.PathwayId, It.IsAny<CancellationToken>())).ReturnsAsync(item.Pathway);
        var log = new RecordingActivityLog();

        var act = () => new GetPathwayMdtItemsHandler(Repository.Object, pathways.Object, Walls.Boundary, log, Clock)
            .Handle(new GetPathwayMdtItemsQuery(item.PathwayId, In(Clinician, LabTeam)), Token);

        await act.Should().ThrowAsync<NotFoundException>();
        log.Views.Should().BeEmpty();
    }

    [Test]
    public void Visible_ShowsTheMdtsTeamItsItems_AndOthersOnlyThoseOnPathwaysTheySee()
    {
        var onOurMdt = GynaecologyCaseOnDermatologysMdt();
        var gynaecologyMdt = CreateMdt(Now.AddDays(1));
        gynaecologyMdt.TeamId = Gynaecology;
        var onTheirMdt = CreateItem(gynaecologyMdt, pathway: onOurMdt.Pathway);
        onTheirMdt.TeamId = Gynaecology;
        var unrelated = CreateItem(gynaecologyMdt);
        unrelated.TeamId = Gynaecology;
        List<MdtItem> all = [onOurMdt, onTheirMdt, unrelated];

        List<Guid> Seen(TeamScope scope) =>
            all.AsQueryable().Visible(scope, Walls.Grants.AsQueryable()).Select(i => i.Id).ToList();

        Seen(TeamWallsTestBoundary.StaffIn(Staff.UserId, Dermatology)).Should().Equal(onOurMdt.Id);
        Seen(TeamWallsTestBoundary.StaffIn(Staff.UserId, Gynaecology)).Should().Equal(onOurMdt.Id, onTheirMdt.Id, unrelated.Id);
        Seen(TeamWallsTestBoundary.StaffIn(Staff.UserId, LabTeam)).Should().BeEmpty();
        Seen(TeamWallsTestBoundary.AdminScope(Admin.UserId)).Should().HaveCount(3);
    }

    /// <summary>A username lookup that knows nobody (the outcome's present list then says "Deleted user").</summary>
    private sealed class IdentityStub : Mock<MedApp.Application.Common.Identity.IIdentityReadService>
    {
        public IdentityStub() =>
            Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string>());
    }
}
