using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.Audit.Queries.SearchProcedureAudit;

[TestFixture]
public sealed class SearchProcedureAuditHandlerTests
{
    private Mock<IProcedureAuditRepository> _repository = null!;
    private SearchProcedureAuditHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IProcedureAuditRepository>();
        fixture.Inject(_repository.Object);
        _handler = fixture.Create<SearchProcedureAuditHandler>();
    }

    [Test]
    public async Task Handle_Entries_MapsThemWithTheActionNameAndTotals()
    {
        var query = CreateAuditSearchQuery();
        var entry = CreateAuditEntry(PatientProcedureAuditAction.StageFinished, Now, stageName: ConsultationStage);
        _repository
            .Setup(r => r.SearchAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PatientProcedureAuditEntry>([entry], 11));

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
            .ReturnsAsync(new PagedResult<PatientProcedureAuditEntry>([], 0));
        using var cts = new CancellationTokenSource();

        await _handler.Handle(query, cts.Token);

        _repository.Verify(r => r.SearchAsync(query, cts.Token), Times.Once);
    }
}
