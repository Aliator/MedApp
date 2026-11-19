using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Results.Commands.MarkResultEnteredInError;
using MedApp.Application.Results.Commands.RecordExternalResult;
using MedApp.Application.Results.Commands.SignOffResult;
using MedApp.Application.Results.Common;
using MedApp.Application.Results.Queries.GetResult;
using MedApp.Application.Results.Queries.SearchResults;
using MedApp.Domain.Activity;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Results;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.ResultsTestConstants;

namespace MedApp.UnitTests.Application.Results;

/// <summary>
/// The team walls on results (MC5, MC6; the light demo's <c>labResults.ts</c>): a result follows its pathway, or its own
/// team. Another team's is a 404, recorded, to read or change; the lab reads (and corrects its own report on) a pathway
/// passed to it; lists and the count leave out what the viewer's teams may not see; the clinic picker refuses another
/// team (a 400) for anyone but an admin.
/// </summary>
[TestFixture]
public sealed class ResultTeamWallsTests : ResultTestBase
{
    private static readonly Guid Dermatology = Guid.Parse("0000005b-0000-0000-0000-0000000000d1");
    private static readonly Guid Gynaecology = Guid.Parse("0000005b-0000-0000-0000-0000000000d2");

    private static PathwayActor In(PathwayActor actor, params Guid[] teams) =>
        actor with { Scope = TeamWallsTestBoundary.StaffIn(actor.UserId, teams) };

    /// <summary>A result on a Gynaecology pathway, with the owner's grant written as the pathway was made.</summary>
    private Result GynaecologyResult(ResultStatus status = ResultStatus.AwaitingSignOff)
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        pathway.TeamId = Gynaecology;
        Walls.Grants.Add(PathwayAccess.Owner(pathway.Id, pathway.PatientId, Gynaecology, Now));
        return Given(CreateResult(pathway, status));
    }

    private GetResultHandler GetHandler(RecordingActivityLog log) =>
        new(Repository.Object, Walls.Boundary, log, Identity.Object, Holders.Object, Clock);

    private static string AccessIn(string details) => JsonDocument.Parse(details).RootElement.GetProperty("access").GetString()!;

    [Test]
    public async Task Get_AnotherTeamsResult_IsA404AsIfItWerentThere_AndIsRecorded()
    {
        var result = GynaecologyResult();
        var log = new RecordingActivityLog();

        var act = () => GetHandler(log).Handle(new GetResultQuery(result.Id, In(Clinician, Dermatology)), Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage(ResultRepositoryExtensions.NotFound(result.Id).Message);
        Walls.ShouldHaveRefused(ResultRepositoryExtensions.TeamWallTarget, result.Id);
        log.SingleViews.Should().BeEmpty("nothing was shown");
    }

    [Test]
    public async Task Get_OwnTeamsResult_RecordsTheViewWithWhy()
    {
        var result = GynaecologyResult();
        var log = new RecordingActivityLog();

        await GetHandler(log).Handle(new GetResultQuery(result.Id, In(Clinician, Gynaecology)), Cts.Token);

        AccessIn(log.SingleViews.Should().ContainSingle().Subject.Details).Should().Be(nameof(TeamAccessKind.Owner));
    }

    [Test]
    public async Task Get_TheLabOnAPathwayPassedToIt_SeesItAsTheStageOwner()
    {
        var result = GynaecologyResult();
        Walls.Grants.Add(PathwayAccess.StageOwner(result.PathwayId!.Value, result.PatientId, Team.LabId, Now));
        var log = new RecordingActivityLog();

        await GetHandler(log).Handle(new GetResultQuery(result.Id, In(SpecimensTestConstants.LabStaff, Team.LabId)), Cts.Token);

        AccessIn(log.SingleViews.Single().Details).Should().Be(nameof(TeamAccessKind.StageOwner));
    }

    [Test]
    public async Task Get_AnAdmin_SeesEveryTeamsResult()
    {
        var result = GynaecologyResult();
        var log = new RecordingActivityLog();
        var admin = SpecimensTestConstants.Corrector with { Scope = TeamWallsTestBoundary.AdminScope(Admin.UserId) };

        await GetHandler(log).Handle(new GetResultQuery(result.Id, admin), Cts.Token);

        AccessIn(log.SingleViews.Single().Details).Should().Be(nameof(TeamAccessKind.Admin));
    }

    [Test]
    public async Task SignOff_AnotherTeamsResult_IsA404_AndChangesNothing()
    {
        var result = GynaecologyResult();
        var handler = new SignOffResultHandler(Repository.Object, Walls.Boundary, Identity.Object, Holders.Object, Notifications.Object, Clock);

        var act = () => handler.Handle(
            new SignOffResultCommand(result.Id, nameof(ResultAction.NoFurtherAction), null, 1, In(Clinician, Dermatology)), Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
        result.Status.Should().Be(ResultStatus.AwaitingSignOff);
        SavedAudit.Should().BeEmpty();
        Walls.ShouldHaveRefused(ResultRepositoryExtensions.TeamWallTarget, result.Id);
    }

    [Test]
    public async Task SignOff_ByAnAdminWhoSeesIt_IsA409NamingItsTeam_AndChangesNothing()
    {
        var result = GynaecologyResult();
        Walls.TeamNames[Gynaecology] = "Synthetic gynaecology";
        var admin = Clinician with { Scope = TeamWallsTestBoundary.AdminScope(Admin.UserId) };
        var handler = new SignOffResultHandler(Repository.Object, Walls.Boundary, Identity.Object, Holders.Object, Notifications.Object, Clock);

        var act = () => handler.Handle(
            new SignOffResultCommand(result.Id, nameof(ResultAction.NoFurtherAction), null, 1, admin), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(TeamWalls.RecordNotYours("This result", "Synthetic gynaecology"));
        result.Status.Should().Be(ResultStatus.AwaitingSignOff);
        SavedAudit.Should().BeEmpty();
    }

    [Test]
    public async Task EnteredInError_TheLabsOwnReport_IsAllowedOnAPathwayPassedToIt()
    {
        // As the light demo: whoever sees a result may act on it by their permissions; the lab corrects its own report.
        var result = GynaecologyResult();
        Walls.Grants.Add(PathwayAccess.StageOwner(result.PathwayId!.Value, result.PatientId, Team.LabId, Now));
        var handler = new MarkResultEnteredInErrorHandler(Repository.Object, Walls.Boundary, Identity.Object, Holders.Object, Clock);

        await handler.Handle(
            new MarkResultEnteredInErrorCommand(result.Id, ErrorDetail, 1, In(SpecimensTestConstants.LabStaff, Team.LabId)), Cts.Token);

        result.Status.Should().Be(ResultStatus.EnteredInError);
    }

    [Test]
    public void Filter_LeavesOutResultsTheViewersTeamsMayNotSee()
    {
        var own = GynaecologyResult();
        var passed = GynaecologyResult();
        Walls.Grants.Add(PathwayAccess.StageOwner(passed.PathwayId!.Value, passed.PatientId, Team.LabId, Now));
        var noPathway = new Result
        {
            Id = Guid.NewGuid(), PatientId = own.PatientId, Patient = own.Patient, TeamId = Team.LabId, Title = ExternalTitle,
            Source = ResultSource.ExternalReport, ReportedAtUtc = Now, ResponsibleUserId = Clinician.UserId, Version = 1,
        };
        List<Result> all = [own, passed, noPathway];

        List<Guid> Seen(TeamScope scope) =>
            all.AsQueryable()
                .Filter(new SearchResultsQuery(1, 10, null, null, null, null, Clinician.UserId, Scope: scope), SignOffHolders, Walls.Grants.AsQueryable())
                .Select(r => r.Id).ToList();

        Seen(TeamWallsTestBoundary.StaffIn(Staff.UserId, Gynaecology)).Should().BeEquivalentTo([own.Id, passed.Id]);
        Seen(TeamWallsTestBoundary.StaffIn(OtherUserId, Team.LabId)).Should().BeEquivalentTo([passed.Id, noPathway.Id]);
        Seen(TeamWallsTestBoundary.StaffIn(Staff.UserId, Dermatology)).Should().BeEmpty();
        Seen(TeamWallsTestBoundary.AdminScope(Admin.UserId)).Should().HaveCount(3);
    }

    [Test]
    public async Task Search_PickingAnotherTeam_IsA400OnTeamId_ForStaff()
    {
        var handler = new SearchResultsHandler(Repository.Object, Holders.Object, Identity.Object, new RecordingActivityLog(), Clock);

        var act = () => handler.Handle(
            new SearchResultsQuery(1, 10, null, Gynaecology, null, null, Staff.UserId,
                Scope: TeamWallsTestBoundary.StaffIn(Staff.UserId, Dermatology)), Cts.Token);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should()
            .ContainSingle(e => e.PropertyName == "TeamId" && e.ErrorMessage == TeamPicker.NotYourTeam);
        Repository.Verify(r => r.SearchAsync(It.IsAny<SearchResultsQuery>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Search_AnAdminPickingAnyTeam_IsListed_WithTheirAccessAndTheTeamPicked()
    {
        var result = GynaecologyResult();
        Repository
            .Setup(r => r.SearchAsync(It.IsAny<SearchResultsQuery>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Result>([result], 1));
        var log = new RecordingActivityLog();

        await new SearchResultsHandler(Repository.Object, Holders.Object, Identity.Object, log, Clock).Handle(
            new SearchResultsQuery(1, 10, null, Gynaecology, null, null, Admin.UserId,
                Scope: TeamWallsTestBoundary.AdminScope(Admin.UserId)), Cts.Token);

        var details = JsonDocument.Parse(log.ViewDetails.Should().ContainSingle().Subject).RootElement;
        details.GetProperty("access").GetString().Should().Be(nameof(TeamAccessKind.Admin));
        details.GetProperty("teamFilter").GetString().Should().Be(Gynaecology.ToString());
    }

    [Test]
    public async Task Count_CountsOnlyWhatTheViewersTeamsMaySee()
    {
        var scope = TeamWallsTestBoundary.StaffIn(Staff.UserId, Dermatology);
        Repository.Setup(r => r.CountAwaitingForAsync(Staff.UserId, scope, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var count = await new SearchResultsHandler(Repository.Object, Holders.Object, Identity.Object, new RecordingActivityLog(), Clock)
            .Handle(new CountResultsQuery(Staff.UserId, scope), Cts.Token);

        count.AwaitingMine.Should().Be(2);
    }

    // Recording a result from elsewhere.

    private (RecordExternalResultHandler Handler, PatientPathway Pathway) ExternalSetUp(Guid pathwayTeam)
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        pathway.TeamId = pathwayTeam;
        Walls.Grants.Add(PathwayAccess.Owner(pathway.Id, pathway.PatientId, pathwayTeam, Now));
        var patients = new Mock<IPatientRepository>();
        patients.Setup(p => p.GetByIdAsync(pathway.PatientId, It.IsAny<CancellationToken>())).ReturnsAsync(pathway.Patient);
        var pathways = new Mock<IPatientPathwayRepository>();
        pathways.Setup(p => p.GetByIdAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);
        var handler = new RecordExternalResultHandler(
            Repository.Object, Walls.Boundary, patients.Object, pathways.Object, Identity.Object, Holders.Object, Notifications.Object, Clock);
        return (handler, pathway);
    }

    private Attachment PatientsFile(PatientPathway pathway, Guid team)
    {
        var file = new Attachment
        {
            Id = Guid.NewGuid(), PatientId = pathway.PatientId, TeamId = team, FileName = "synthetic-scan.pdf",
            ContentType = "application/pdf", UploadedByUserId = Staff.UserId, UploadedAtUtc = Now.AddDays(-1),
        };
        Repository.Setup(r => r.GetReportAsync(file.Id, It.IsAny<CancellationToken>())).ReturnsAsync(file);
        return file;
    }

    [Test]
    public async Task RecordExternal_OnAPathwayBehindTheWall_IsTheSame400AsAnotherPatients_AndIsRecorded()
    {
        var (handler, pathway) = ExternalSetUp(Gynaecology);
        var file = PatientsFile(pathway, Dermatology);

        var act = () => handler.Handle(
            new RecordExternalResultCommand(file.Id, ExternalTitle, pathway.Id, Clinician.UserId, In(Clinician, Dermatology)), Cts.Token);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should()
            .ContainSingle(e => e.ErrorMessage == RecordExternalResultHandler.NotPatientsPathwayMessage);
        Walls.ShouldHaveRefused(TeamWalls.PathwayTarget, pathway.Id);
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task RecordExternal_OnAPathwayTheViewerSeesButAnotherTeamOwns_IsA409()
    {
        var (handler, pathway) = ExternalSetUp(Gynaecology);
        Walls.Grants.Add(PathwayAccess.StageOwner(pathway.Id, pathway.PatientId, Team.LabId, Now));
        var file = PatientsFile(pathway, Team.LabId);
        var labClinician = In(Clinician, Team.LabId);

        var act = () => handler.Handle(
            new RecordExternalResultCommand(file.Id, ExternalTitle, pathway.Id, Clinician.UserId, labClinician), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(TeamWalls.NotYourTeams(null));
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task RecordExternal_AFileBehindTheWall_IsA404AsIfItWerentThere()
    {
        var (handler, pathway) = ExternalSetUp(Gynaecology);
        var file = PatientsFile(pathway, Gynaecology);

        var act = () => handler.Handle(
            new RecordExternalResultCommand(file.Id, ExternalTitle, null, Clinician.UserId, In(Clinician, Dermatology)), Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
        Walls.ShouldHaveRefused(TeamWalls.AttachmentTarget, file.Id);
    }

    [Test]
    public async Task RecordExternal_OwnTeamsPathway_IsRecordedInItsTeam()
    {
        var (handler, pathway) = ExternalSetUp(Gynaecology);
        var file = PatientsFile(pathway, Gynaecology);

        await handler.Handle(
            new RecordExternalResultCommand(file.Id, ExternalTitle, pathway.Id, Clinician.UserId, In(Clinician, Gynaecology)), Cts.Token);

        Added.Should().ContainSingle().Which.TeamId.Should().Be(Gynaecology);
    }
}
