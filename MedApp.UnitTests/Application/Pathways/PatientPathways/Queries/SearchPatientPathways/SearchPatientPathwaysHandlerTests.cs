using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;

[TestFixture]
public sealed class SearchPatientPathwaysHandlerTests : PathwayHandlerTestBase
{
    [Test]
    public async Task Handle_Results_ReturnsThePageAsSummariesWithTotals()
    {
        var query = CreateSearchQuery() with { PageSize = 2 };
        var pathway = CreatePathway(Status.Assigned);
        Repository
            .Setup(r => r.SearchAsync(query, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PatientPathway>([pathway], 3));
        var handler = Fixture.Create<SearchPatientPathwaysHandler>();

        var result = await handler.Handle(query, CancellationToken.None);

        result.Items.Should().ContainSingle(s => s.Id == pathway.Id && s.Assignees.Single() == Staff.Username);
        result.TotalCount.Should().Be(3);
        result.TotalPages.Should().Be(2);
        result.Page.Should().Be(1);
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        var query = CreateSearchQuery();
        Repository
            .Setup(r => r.SearchAsync(query, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PatientPathway>([], 0));
        using var cts = new CancellationTokenSource();

        await Fixture.Create<SearchPatientPathwaysHandler>().Handle(query, cts.Token);

        Repository.Verify(r => r.SearchAsync(query, Now, cts.Token), Times.Once);
    }
}
