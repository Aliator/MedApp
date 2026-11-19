using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Consent.Commands.MarkConsentEnteredInError;
using MedApp.Application.Consent.Commands.RecordConsent;
using MedApp.Application.Consent.Commands.WithdrawConsent;
using MedApp.Application.Consent.Common;
using MedApp.Application.Consent.Queries.GetPathwayConsentRecords;
using MedApp.Application.Consent.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Consent;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Consent;

/// <summary>
/// The team walls on consent (MC5; the light demo's <c>consent.ts</c>): a pathway's consent records are read only where
/// the pathway is seen (else a 404, recorded), and recorded, withdrawn or corrected only by the pathway's own team
/// (another team that sees it, such as the lab or an admin, gets a 409).
/// </summary>
[TestFixture]
public sealed class ConsentTeamWallsTests
{
    private static readonly Guid Dermatology = Guid.Parse("0000005c-0000-0000-0000-0000000000d1");
    private static readonly Guid Gynaecology = Guid.Parse("0000005c-0000-0000-0000-0000000000d2");

    private Mock<IConsentRepository> _repository = null!;
    private Mock<IPatientPathwayRepository> _pathways = null!;
    private Mock<IIdentityReadService> _identity = null!;
    private TeamWallsTestBoundary _walls = null!;
    private readonly FixedTimeProvider _clock = new(Now);

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IConsentRepository>();
        _repository.Setup(r => r.GetFormAsync(ConsentTestConstants.Form().Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConsentTestConstants.Form());
        _pathways = new Mock<IPatientPathwayRepository>();
        _identity = new Mock<IIdentityReadService>();
        _identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _walls = new TeamWallsTestBoundary();
    }

    private static PathwayActor RecorderIn(params Guid[] teams) =>
        ConsentTestConstants.Recorder with { Scope = TeamWallsTestBoundary.StaffIn(Staff.UserId, teams) };

    /// <summary>A Gynaecology pathway (its owner's grant written), readable through the repository.</summary>
    private PatientPathway GynaecologyPathway()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        pathway.TeamId = Gynaecology;
        _walls.Grants.Add(PathwayAccess.Owner(pathway.Id, pathway.PatientId, Gynaecology, Now));
        _pathways.Setup(p => p.GetByIdAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);
        return pathway;
    }

    private ConsentRecord GynaecologyRecord()
    {
        var record = ConsentTestConstants.CreateRecord(GynaecologyPathway());
        _repository.Setup(r => r.GetForUpdateAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        return record;
    }

    private void PassedToTheLab(PatientPathway pathway) =>
        _walls.Grants.Add(PathwayAccess.StageOwner(pathway.Id, pathway.PatientId, Team.LabId, Now));

    private RecordConsentCommand Record(PatientPathway pathway, PathwayActor actor) =>
        new(pathway.Id, pathway.StageAt(1).Id, ConsentTestConstants.Form().Id, Today, "Patient", null, actor);

    [Test]
    public async Task GetRecords_AnotherTeamsPathway_IsA404AsIfItWerentThere_AndIsRecorded()
    {
        var pathway = GynaecologyPathway();
        var log = new RecordingActivityLog();
        var handler = new GetPathwayConsentRecordsHandler(_pathways.Object, _walls.Boundary, _repository.Object, _identity.Object, log, _clock);

        var act = () => handler.Handle(new GetPathwayConsentRecordsQuery(pathway.Id, RecorderIn(Dermatology)), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage(PathwayRepositoryExtensions.NotFoundMessage(pathway.Id));
        _walls.ShouldHaveRefused(TeamWalls.PathwayTarget, pathway.Id);
        log.Views.Should().BeEmpty();
        _repository.Verify(r => r.GetForPathwayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetRecords_OwnTeamsPathway_RecordsTheViewWithWhy()
    {
        var pathway = GynaecologyPathway();
        _repository.Setup(r => r.GetForPathwayAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var log = new RecordingActivityLog();
        var handler = new GetPathwayConsentRecordsHandler(_pathways.Object, _walls.Boundary, _repository.Object, _identity.Object, log, _clock);

        await handler.Handle(new GetPathwayConsentRecordsQuery(pathway.Id, RecorderIn(Gynaecology)), CancellationToken.None);

        JsonDocument.Parse(log.ViewDetails.Should().ContainSingle().Subject).RootElement
            .GetProperty("access").GetString().Should().Be(nameof(TeamAccessKind.Owner));
    }

    [Test]
    public async Task Record_OnAnotherTeamsPathway_IsA404_OrA409WhereTheViewerSeesIt()
    {
        var pathway = GynaecologyPathway();
        var handler = new RecordConsentHandler(_pathways.Object, _walls.Boundary, _repository.Object, _identity.Object, _clock);

        await FluentActions.Invoking(() => handler.Handle(Record(pathway, RecorderIn(Dermatology)), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
        PassedToTheLab(pathway);
        await FluentActions.Invoking(() => handler.Handle(Record(pathway, RecorderIn(Team.LabId)), CancellationToken.None))
            .Should().ThrowAsync<ConflictException>().WithMessage(TeamWalls.NotYourTeams(null));

        _repository.Verify(r => r.AddAsync(It.IsAny<ConsentRecord>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Record_OwnTeamsPathway_IsRecordedInItsTeam()
    {
        var pathway = GynaecologyPathway();
        ConsentRecord? added = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<ConsentRecord>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((ConsentRecord record, IEnumerable<PatientPathwayAuditEntry> _, CancellationToken _) => added = record)
            .Returns(Task.CompletedTask);

        await new RecordConsentHandler(_pathways.Object, _walls.Boundary, _repository.Object, _identity.Object, _clock)
            .Handle(Record(pathway, RecorderIn(Gynaecology)), CancellationToken.None);

        added!.TeamId.Should().Be(Gynaecology);
    }

    [Test]
    public async Task Withdraw_AnotherTeamsRecord_IsA404AsIfItWerentThere_AndIsRecorded()
    {
        var record = GynaecologyRecord();
        var handler = new WithdrawConsentHandler(_repository.Object, _walls.Boundary, _identity.Object, _clock);

        var act = () => handler.Handle(new WithdrawConsentCommand(record.Id, Today, "Synthetic.", 1, RecorderIn(Dermatology)), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage(ConsentRecords.NotFoundMessage(record.Id));
        _walls.ShouldHaveRefused(ConsentRecords.TeamWallTarget, record.Id);
        record.Status.Should().Be(ConsentStatus.Valid);
    }

    [Test]
    public async Task Withdraw_ByTheLabOnAPathwayPassedToIt_IsA409()
    {
        var record = GynaecologyRecord();
        PassedToTheLab(record.Pathway);
        var handler = new WithdrawConsentHandler(_repository.Object, _walls.Boundary, _identity.Object, _clock);

        var act = () => handler.Handle(new WithdrawConsentCommand(record.Id, Today, "Synthetic.", 1, RecorderIn(Team.LabId)), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(TeamWalls.NotYourTeams(null));
        record.Status.Should().Be(ConsentStatus.Valid);
    }

    [Test]
    public async Task EnteredInError_ByAnAdmin_IsA409_AsOnlyThePathwaysTeamChangesItsConsent()
    {
        var record = GynaecologyRecord();
        var admin = ConsentTestConstants.Corrector with { Scope = TeamWallsTestBoundary.AdminScope(Admin.UserId) };
        var handler = new MarkConsentEnteredInErrorHandler(_repository.Object, _walls.Boundary, _identity.Object, _clock);

        var act = () => handler.Handle(new MarkConsentEnteredInErrorCommand(record.Id, "Synthetic.", 1, admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(TeamWalls.NotYourTeams(null));
        record.Status.Should().Be(ConsentStatus.Valid);
    }
}
