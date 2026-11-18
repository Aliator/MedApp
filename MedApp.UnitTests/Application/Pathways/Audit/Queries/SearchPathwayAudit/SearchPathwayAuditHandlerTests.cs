using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Audit.Queries.SearchPathwayAudit;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.Audit.Queries.SearchPathwayAudit;

[TestFixture]
public sealed class SearchPathwayAuditHandlerTests
{
    private Mock<IPathwayAuditRepository> _repository = null!;
    private SearchPathwayAuditHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPathwayAuditRepository>();
        fixture.Inject(_repository.Object);
        _handler = fixture.Create<SearchPathwayAuditHandler>();
    }

    [Test]
    public async Task Handle_Entries_MapsThemWithTheActionNameAndTotals()
    {
        var query = CreateAuditSearchQuery();
        var entry = CreateAuditEntry(PatientPathwayAuditAction.StageFinished, Now, stageName: ConsultationStage);
        _repository
            .Setup(r => r.SearchAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PatientPathwayAuditEntry>([entry], 11));

        var result = await _handler.Handle(query, CancellationToken.None);

        var item = result.Items.Should().ContainSingle().Subject;
        item.Action.Should().Be("StageFinished");
        item.StageName.Should().Be(ConsultationStage);
        item.Username.Should().Be(entry.Username);
        item.Details.Should().Be("{}");
        result.TotalCount.Should().Be(11);
        result.TotalPages.Should().Be(2);
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        var query = CreateAuditSearchQuery();
        _repository
            .Setup(r => r.SearchAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PatientPathwayAuditEntry>([], 0));
        using var cts = new CancellationTokenSource();

        await _handler.Handle(query, cts.Token);

        _repository.Verify(r => r.SearchAsync(query, cts.Token), Times.Once);
    }
}
