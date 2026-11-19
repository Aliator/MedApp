using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Consults.Queries.GetIncomingConsults;
using MedApp.Application.Consults.Queries.GetPathwayConsults;
using MedApp.Application.Consults.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Consults;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ConsultsTestConstants;

namespace MedApp.UnitTests.Application.Consults.Queries;

[TestFixture]
public sealed class GetPathwayConsultsHandlerTests
{
    private readonly CancellationToken _token = new CancellationTokenSource().Token;
    private Mock<IConsultRepository> _repository = null!;
    private TeamWallsTestBoundary _walls = null!;
    private PatientPathway _pathway = null!;

    [SetUp]
    public void SetUp()
    {
        _pathway = CreateDermatologyPathway();
        _walls = new TeamWallsTestBoundary();
        _walls.Grants.AddRange(Grants(_pathway));
        _repository = new Mock<IConsultRepository>();
        _repository.Setup(r => r.GetPathwayAsync(_pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_pathway);
        _repository.Setup(r => r.GetForPathwayAsync(_pathway.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateConsult(_pathway, ConsultStatus.Replied), CreateConsult(_pathway, ConsultStatus.Withdrawn)]);
    }

    private GetPathwayConsultsHandler Handler() => new(_repository.Object, _walls.Boundary, _walls.Activity);

    [Test]
    public async Task Handle_OwnTeam_ListsThemCanRequestAndRecordsTheViewAsOwner()
    {
        var response = await Handler().Handle(new GetPathwayConsultsQuery(_pathway.Id, Asker), _token);

        response.Consults.Select(c => c.Status).Should().Equal("Replied", "Withdrawn");
        response.CanRequest.Should().BeTrue();
        var view = _walls.Activity.Views.Should().ContainSingle().Subject;
        view.Action.Should().Be(ActivityAction.PathwayViewed);
        view.PatientIds.Should().Equal(_pathway.PatientId);
        view.PathwayId.Should().Be(_pathway.Id);
        _walls.Activity.ViewDetails.Should().ContainSingle().Which.Should().Contain("\"access\":\"Owner\"");
    }

    [Test]
    public async Task Handle_TheAskedTeam_ReadsThemThroughTheConsult_ButCantRequest()
    {
        var response = await Handler().Handle(new GetPathwayConsultsQuery(_pathway.Id, Pathologist), _token);

        response.CanRequest.Should().BeFalse();
        _walls.Activity.ViewDetails.Should().ContainSingle().Which.Should().Contain("\"access\":\"Consult\"");
    }

    [Test]
    public async Task Handle_AfterTheConsultClosed_TheAskedTeamGetsARecordedNotFound()
    {
        _walls.Grants.RemoveAll(g => g.TeamId == Lab.Id);

        await FluentActions.Awaiting(() => Handler().Handle(new GetPathwayConsultsQuery(_pathway.Id, Pathologist), _token))
            .Should().ThrowAsync<NotFoundException>();
        _walls.ShouldHaveRefused(TeamWalls.PathwayTarget, _pathway.Id);
        _walls.Activity.Views.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NoSuchPathway_IsNotFound() =>
        await FluentActions.Awaiting(() => Handler().Handle(new GetPathwayConsultsQuery(Guid.NewGuid(), Asker), _token))
            .Should().ThrowAsync<NotFoundException>();
}

[TestFixture]
public sealed class GetIncomingConsultsHandlerTests
{
    private readonly CancellationToken _token = new CancellationTokenSource().Token;
    private Mock<IConsultRepository> _repository = null!;
    private RecordingActivityLog _activity = null!;

    [SetUp]
    public void SetUp()
    {
        _activity = new RecordingActivityLog();
        _repository = new Mock<IConsultRepository>();
    }

    private GetIncomingConsultsHandler Handler() => new(_repository.Object, _activity);

    [Test]
    public async Task Handle_AskedTeam_ListsItsOpenConsultsAndRecordsEachPatient()
    {
        var pathway = CreateDermatologyPathway();
        var consult = CreateConsult(pathway);
        _repository.Setup(r => r.GetIncomingAsync(null, Pathologist.Teams, _token)).ReturnsAsync([consult, CreateConsult(pathway)]);

        var response = await Handler().Handle(new GetIncomingConsultsQuery(null, Pathologist), _token);

        response.Should().HaveCount(2);
        response[0].CanReply.Should().BeTrue();
        var view = _activity.Views.Should().ContainSingle().Subject;
        view.Action.Should().Be(ActivityAction.PatientListed);
        view.PatientIds.Should().Equal(pathway.PatientId);
    }

    [Test]
    public async Task Handle_AnotherTeamsPick_IsABadRequest()
    {
        await FluentActions.Awaiting(() => Handler().Handle(new GetIncomingConsultsQuery(Dermatology.Id, Pathologist), _token))
            .Should().ThrowAsync<ValidationException>().WithMessage($"*{TeamPicker.NotYourTeam}*");
        _repository.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_AnAdmin_PicksAnyTeam_AndRecordsTheFilter()
    {
        _repository.Setup(r => r.GetIncomingAsync(Lab.Id, AdminViewer.Teams, _token)).ReturnsAsync([]);

        var response = await Handler().Handle(new GetIncomingConsultsQuery(Lab.Id, AdminViewer), _token);

        response.Should().BeEmpty();
        _repository.Verify(r => r.GetIncomingAsync(Lab.Id, AdminViewer.Teams, _token));
        _activity.ViewDetails.Should().ContainSingle().Which.Should().Contain($"\"teamFilter\":\"{Lab.Id}\"");
    }
}
