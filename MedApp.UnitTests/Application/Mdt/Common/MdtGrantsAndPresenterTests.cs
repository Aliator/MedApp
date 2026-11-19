using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Mdt.Commands.ChooseMdtPresenter;
using MedApp.Application.Mdt.Commands.DeferMdtItem;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Calendar;
using MedApp.Domain.Mdt;
using MedApp.Domain.Specimens;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Application.Mdt.Commands;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static MedApp.UnitTests.Common.Constants.MdtTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Mdt.Common;

/// <summary>MDT attendees' grants (MC26), their read-only reasons, and MC21's presenter and lab work.</summary>
[TestFixture]
public sealed class MdtGrantsTests
{
    [Test]
    public void Holders_AreTheAttendees_WhileTheItemIsOpen_OrTheMeetingHasntEnded()
    {
        var meeting = CreateMdt(Now.AddMinutes(-30));
        var attendees = meeting.Attendees.Select(a => a.StaffUserId).ToList();

        MdtGrants.Holders(CreateItem(meeting), Now).Should().BeEquivalentTo(attendees);
        MdtGrants.Holders(CreateItem(meeting, MdtItemStatus.Discussed), Now).Should().BeEquivalentTo(attendees, "the meeting hasn't ended");
        MdtGrants.Holders(CreateItem(meeting, MdtItemStatus.Withdrawn), Now.AddHours(2)).Should().BeEmpty();
        MdtGrants.Holders(CreateItem(meeting), Now.AddHours(2)).Should().BeEquivalentTo(attendees, "an open item keeps its readers");
        MdtGrants.Holders(CreateItem(null), Now).Should().BeEmpty("a cancelled MDT gives nobody a grant");
    }

    [Test]
    public void Sync_GivesNewAttendeesTheirGrants_EndsTheRemovedOnes_AndKeepsTheRest()
    {
        var meeting = CreateMdt(Now.AddDays(1));
        var item = CreateItem(meeting);
        var kept = PathwayAccess.Mdt(item.PathwayId, item.PatientId, Staff.UserId, item.Id, Now.AddDays(-1));
        var removed = PathwayAccess.Mdt(item.PathwayId, item.PatientId, Guid.NewGuid(), item.Id, Now.AddDays(-1));
        var otherItems = PathwayAccess.Mdt(item.PathwayId, item.PatientId, Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-1));

        var added = MdtGrants.Sync(item, [kept, removed, otherItems], Now);

        added.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            PathwayId = item.PathwayId, PatientId = item.PatientId, UserId = (Guid?)OtherUserId, MdtItemId = (Guid?)item.Id,
            Reason = PathwayAccessReason.Mdt, TeamId = (Guid?)null, EndedAtUtc = (DateTime?)null,
        });
        kept.EndedAtUtc.Should().BeNull();
        removed.EndedAtUtc.Should().Be(Now);
        otherItems.EndedAtUtc.Should().BeNull("another item's grants are its own");
    }

    [Test]
    public async Task Job_EndsTheLapsedGrants_AndSaysHowMany()
    {
        var repository = new Mock<IMdtItemRepository>();
        repository.Setup(r => r.EndLapsedGrantsAsync(Now, It.IsAny<CancellationToken>())).ReturnsAsync(3);
        var job = new MdtGrantsJob(repository.Object, NullLogger<MdtGrantsJob>.Instance);

        (await job.RunAsync(Now, CancellationToken.None)).Should().Be(3);
        job.Name.Should().Be(MdtGrantsJob.JobName);
    }

    [Test]
    public void AnAttendeeFromAnotherTeam_IsToldWhyItsReadOnly_TheTeamAndAdminsArent()
    {
        var meeting = CreateMdt(Now.AddHours(1));
        var item = CreateItem(meeting);
        var pathologist = TeamWallsTestBoundary.StaffIn(OtherUserId, MedApp.Domain.Teams.Team.LabId);

        MdtRules.AttendeeReadOnly(item, pathologist).Should()
            .Be("Dermatology’s case: you attend its MDT, so you read it until it’s discussed or withdrawn. Only its team changes it.");
        MdtRules.AttendeeReadOnly(item, TeamWallsTestBoundary.StaffIn(Staff.UserId, MedApp.UnitTests.Common.Constants.MdtTestConstants.Team.Id)).Should().BeNull();
        MdtRules.AttendeeReadOnly(item, TeamWallsTestBoundary.AdminScope(Admin.UserId)).Should().BeNull();

        MdtRules.ListReadOnlyReason(meeting, "Dermatology", pathologist).Should().StartWith("You attend Dermatology’s MDT, so you can read the cases listed");
        MdtRules.ListReadOnlyReason(meeting, "Dermatology", TeamWallsTestBoundary.StaffIn(Guid.NewGuid(), Guid.NewGuid())).Should()
            .Be("This is Dermatology’s MDT and you aren’t invited: only its team and its attendees see the cases listed on it.");
        MdtRules.ListReadOnlyReason(meeting, "Dermatology", TeamWallsTestBoundary.StaffIn(Staff.UserId, MedApp.UnitTests.Common.Constants.MdtTestConstants.Team.Id)).Should().BeNull();
    }

    [Test]
    public void APathwayReadThroughAnMdt_SaysSo()
    {
        var pathway = CreatePathway(MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus.InProgress);

        var response = PathwayMapper.ToResponse(
            pathway, new Dictionary<Guid, string>(), Now, viewerScope: TeamWallsTestBoundary.StaffIn(OtherUserId, MedApp.Domain.Teams.Team.LabId),
            teamName: "Dermatology", viewerAccess: TeamAccessKind.Mdt);

        response.ReadOnlyReason.Should().Be(TeamWalls.MdtReadOnly("Dermatology"));
    }

    [Test]
    public void Mapper_GivesThePresenter_TheLabWorkOnItsPathway_AndWhoMayChoose()
    {
        var meeting = CreateMdt(Now.AddHours(1));
        var item = CreateItem(meeting);
        item.PresenterUserId = OtherUserId;
        item.PresenterUsername = "test.pathologist";
        var resultId = Guid.NewGuid();
        MdtLabWork[] work =
        [
            new(item.PathwayId, Guid.NewGuid(), "MA26-000010", "Skin biopsy (punch)", SpecimenStatus.Reported, Now.AddDays(-1), resultId),
            new(Guid.NewGuid(), Guid.NewGuid(), "MA26-000011", "Urine", SpecimenStatus.Taken, null, null),
        ];
        var ownTeam = new PathwayActor(Nurse.UserId, Nurse.Username, Nurse.Permissions,
            TeamWallsTestBoundary.StaffIn(Nurse.UserId, MedApp.UnitTests.Common.Constants.MdtTestConstants.Team.Id));
        var pathologist = new PathwayActor(OtherUserId, "test.pathologist", Nurse.Permissions, TeamWallsTestBoundary.StaffIn(OtherUserId, MedApp.Domain.Teams.Team.LabId));

        var response = MdtMapper.ToResponse(item, ownTeam, Now, work);

        response.PresenterUserId.Should().Be(OtherUserId);
        response.PresenterUsername.Should().Be("test.pathologist");
        response.CanChoosePresenter.Should().BeTrue();
        response.LabWork.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Number = "MA26-000010", TypeName = "Skin biopsy (punch)", Status = "Reported", ResultId = (Guid?)resultId,
        });
        response.ReadOnlyReason.Should().BeNull();
        MdtMapper.ToResponse(item, pathologist, Now, work).CanChoosePresenter.Should().BeFalse("only the item's team chooses");
        MdtMapper.ToResponse(CreateItem(null), ownTeam, Now).CanChoosePresenter.Should().BeFalse("its meeting was cancelled");
    }
}

[TestFixture]
public sealed class ChooseMdtPresenterHandlerTests : MdtCommandTestBase
{
    private Meeting _meeting = null!;
    private MdtItem _item = null!;
    private Mock<IIdentityReadService> _identity = null!;

    private static readonly PathwayActor TeamNurse = new(
        Nurse.UserId, Nurse.Username, Nurse.Permissions,
        TeamWallsTestBoundary.StaffIn(Nurse.UserId, MedApp.UnitTests.Common.Constants.MdtTestConstants.Team.Id));

    [SetUp]
    public void SetUp()
    {
        _meeting = CreateMdt(Now.AddDays(1));
        _item = CreateItem(_meeting);
        Given(_item);
        Walls.Grants.Add(PathwayAccess.Owner(_item.PathwayId, _item.PatientId, MedApp.UnitTests.Common.Constants.MdtTestConstants.Team.Id, Now));
        Walls.TeamNames[MedApp.UnitTests.Common.Constants.MdtTestConstants.Team.Id] = "Dermatology";
        Repository.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _identity = new Mock<IIdentityReadService>();
        _identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [OtherUserId] = "test.pathologist" });
    }

    private ChooseMdtPresenterHandler Handler() => new(Repository.Object, Walls.Boundary, _identity.Object, Clock);

    [Test]
    public async Task AnAttendee_IsChosen_AndNobodyClearsIt()
    {
        var response = await Handler().Handle(new ChooseMdtPresenterCommand(_item.Id, OtherUserId, 1, TeamNurse), Token);

        response.PresenterUserId.Should().Be(OtherUserId);
        response.PresenterUsername.Should().Be("test.pathologist");
        response.Version.Should().Be(2);
        Repository.Verify(r => r.SaveAsync(Token), Times.Once);
        Audit.Should().BeEmpty("choosing a presenter writes no audit entry");

        var cleared = await Handler().Handle(new ChooseMdtPresenterCommand(_item.Id, null, 2, TeamNurse), Token);
        cleared.PresenterUserId.Should().BeNull();
        cleared.PresenterUsername.Should().BeNull();
    }

    [Test]
    public async Task IsRefused_ForSomeoneNotInvited_AStaleVersion_ACancelledMeeting_OrAnotherTeam()
    {
        await FluentActions.Awaiting(() => Handler().Handle(new ChooseMdtPresenterCommand(_item.Id, Guid.NewGuid(), 1, TeamNurse), Token))
            .Should().ThrowAsync<ValidationException>().WithMessage($"*{MdtRules.PresenterNotAttendingMessage}*");
        await FluentActions.Awaiting(() => Handler().Handle(new ChooseMdtPresenterCommand(_item.Id, OtherUserId, 5, TeamNurse), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(MdtRules.ChangedMeanwhileMessage);

        var attendee = new PathwayActor(OtherUserId, "test.pathologist", Nurse.Permissions, TeamWallsTestBoundary.StaffIn(OtherUserId, MedApp.Domain.Teams.Team.LabId));
        Walls.Grants.Add(PathwayAccess.Mdt(_item.PathwayId, _item.PatientId, OtherUserId, _item.Id, Now));
        await FluentActions.Awaiting(() => Handler().Handle(new ChooseMdtPresenterCommand(_item.Id, OtherUserId, 1, attendee), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage("This MDT item is Dermatology’s*");

        _item.MeetingId = null;
        _item.Meeting = null;
        await FluentActions.Awaiting(() => Handler().Handle(new ChooseMdtPresenterCommand(_item.Id, OtherUserId, 1, TeamNurse), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(MdtRules.NoMeetingMessage);
        Repository.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void Validator_NeedsTheItemAndVersion()
    {
        var validator = new ChooseMdtPresenterValidator();
        validator.Validate(new ChooseMdtPresenterCommand(_item.Id, null, 1, TeamNurse)).IsValid.Should().BeTrue();
        validator.Validate(new ChooseMdtPresenterCommand(_item.Id, Guid.Empty, 0, TeamNurse)).Errors.Should().HaveCount(2);
    }

    [Test]
    public async Task Defer_ToAnMdtThePresenterIsntInvitedTo_DropsThem_AndSyncsTheGrants()
    {
        _item.PresenterUserId = OtherUserId;
        _item.PresenterUsername = "test.pathologist";
        var later = CreateMdt(Now.AddDays(8));
        later.Attendees.RemoveAll(a => a.StaffUserId == OtherUserId);
        Given(later);

        await new DeferMdtItemHandler(Repository.Object, Walls.Boundary, Teams.Object, Clock)
            .Handle(new DeferMdtItemCommand(_item.Id, later.Id, 1, TeamNurse), Token);

        _item.PresenterUserId.Should().BeNull();
        _item.PresenterUsername.Should().BeNull();
        Repository.Verify(r => r.SyncGrantsAsync(
            It.Is<IReadOnlyCollection<MdtItem>>(items => items.Single() == _item && _item.Meeting == later), Now, Token), Times.Once);
    }
}
