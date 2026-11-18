using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Queries.GetLetterMergeFields;
using MedApp.Application.Letters.Queries.GetLetterTemplateById;
using MedApp.Application.Letters.Queries.GetLetterTemplates;
using MedApp.Application.Letters.Queries.PreviewLetterTemplate;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.LettersTestConstants;
using static MedApp.UnitTests.Common.Constants.TeamsTestConstants;

namespace MedApp.UnitTests.Application.Letters.Queries;

[TestFixture]
public sealed class LetterTemplateQueriesTests
{
    private CancellationTokenSource _cts = null!;
    private Mock<ILetterTemplateRepository> _repository = null!;
    private Mock<ITeamRepository> _teams = null!;
    private Team _urology = null!;
    private Team _dermatology = null!;

    [SetUp]
    public void SetUp()
    {
        _cts = new CancellationTokenSource();
        _repository = new Mock<ILetterTemplateRepository>();
        _repository.Setup(r => r.GetInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
        _urology = CreateTeam(OtherTeamName);
        _dermatology = CreateTeam(TeamName);
        _teams = new Mock<ITeamRepository>();
        _teams.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                new[] { _urology, _dermatology }.Where(s => ids.Contains(s.Id)).ToList());
        _teams.Setup(r => r.GetAsync(_urology.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_urology);
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    [Test]
    public async Task GetAll_EveryTeamsFirst_ThenByTeamName_ThenByName()
    {
        var urology = CreateTemplate("A urology letter", _urology.Id);
        var dermatologyB = CreateTemplate("b dermatology letter", _dermatology.Id);
        var dermatologyA = CreateTemplate("A dermatology letter", _dermatology.Id);
        var everyone = CreateTemplate("Z for everyone");
        _repository.Setup(r => r.GetAllAsync(true, _cts.Token)).ReturnsAsync([urology, dermatologyB, everyone, dermatologyA]);

        var result = await new GetLetterTemplatesHandler(_repository.Object, _teams.Object)
            .Handle(new GetLetterTemplatesQuery(true), _cts.Token);

        result.Select(t => t.Id).Should().Equal(everyone.Id, dermatologyA.Id, dermatologyB.Id, urology.Id);
        result[1].TeamName.Should().Be(TeamName);
    }

    [Test]
    public async Task GetById_ReturnsIt_WithItsTeamsName()
    {
        var template = CreateTemplate(teamId: _urology.Id);
        _repository.Setup(r => r.GetAsync(template.Id, _cts.Token)).ReturnsAsync(template);

        var response = await new GetLetterTemplateByIdHandler(_repository.Object, _teams.Object)
            .Handle(new GetLetterTemplateByIdQuery(template.Id), _cts.Token);

        response.Id.Should().Be(template.Id);
        response.TeamName.Should().Be(OtherTeamName);
    }

    [Test]
    public async Task GetById_ForEveryTeam_LooksUpNoTeam()
    {
        var template = CreateTemplate();
        _repository.Setup(r => r.GetAsync(template.Id, _cts.Token)).ReturnsAsync(template);

        var response = await new GetLetterTemplateByIdHandler(_repository.Object, _teams.Object)
            .Handle(new GetLetterTemplateByIdQuery(template.Id), _cts.Token);

        response.TeamName.Should().BeNull();
        _teams.Verify(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetById_Unknown_IsNotFound()
    {
        var act = () => new GetLetterTemplateByIdHandler(_repository.Object, _teams.Object)
            .Handle(new GetLetterTemplateByIdQuery(Guid.NewGuid()), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task MergeFields_ListsEveryOne()
    {
        var result = await new GetLetterMergeFieldsHandler().Handle(new GetLetterMergeFieldsQuery(), _cts.Token);

        result.Select(f => f.Name).Should().Equal(LetterMergeFields.All.Select(f => f.Name));
    }

    [Test]
    public async Task Preview_RendersTheTextFilledWithMadeUpDetails_MarkedOnEveryPage()
    {
        var renderer = new Mock<ILetterRenderer>();
        LetterDocument? rendered = null;
        renderer.Setup(r => r.RenderAsync(It.IsAny<LetterDocument>(), It.IsAny<CancellationToken>()))
            .Callback((LetterDocument d, CancellationToken _) => rendered = d)
            .ReturnsAsync([1, 2, 3]);
        var clock = new FixedTimeProvider(new DateTime(2026, 6, 30, 23, 30, 0, DateTimeKind.Utc));

        var pdf = await new PreviewLetterTemplateHandler(renderer.Object, clock).Handle(
            new PreviewLetterTemplateQuery(" Clinic ", "Centre\r\n{{today}}", BodySkeleton, Closing, true), _cts.Token);

        pdf.Should().BeEquivalentTo(new LetterPdf("letter-template-preview.pdf", [1, 2, 3]));
        renderer.Verify(r => r.RenderAsync(It.IsAny<LetterDocument>(), _cts.Token));
        rendered!.Title.Should().Be("Clinic (preview)");
        // UK time: 00:30 on 1 July.
        rendered.Letterhead.Should().Equal("Centre", "1 July 2026");
        rendered.RecipientAddress.Should().Equal("Example Medical Practice", "2 Sample Road", "Exampletown", "ZZ1 2ZZ");
        rendered.Body.Should().EndWith("Re: Sam Example, born 1 January 1970");
        rendered.Closing.Should().Equal("Yours sincerely,", "", "Alex Author", "Consultant Dermatologist");
        rendered.PatientIdentifiers.Should().Be("Sam Example · Born 1 January 1970 · NHS number 999 000 0018");
        rendered.LargePrint.Should().BeTrue();
        rendered.Marking.Should().Be(PreviewLetterTemplateHandler.Marking);
    }

    [Test]
    public async Task Preview_WithoutAName_HasAPlainTitle()
    {
        var renderer = new Mock<ILetterRenderer>();
        LetterDocument? rendered = null;
        renderer.Setup(r => r.RenderAsync(It.IsAny<LetterDocument>(), It.IsAny<CancellationToken>()))
            .Callback((LetterDocument d, CancellationToken _) => rendered = d)
            .ReturnsAsync([]);

        await new PreviewLetterTemplateHandler(renderer.Object, TimeProvider.System).Handle(
            new PreviewLetterTemplateQuery(" ", "", "", "", false), _cts.Token);

        rendered!.Title.Should().Be("Letter template preview");
        rendered.Letterhead.Should().BeEmpty();
        rendered.Closing.Should().BeEmpty();
    }
}
