using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;

[TestFixture]
public sealed class SearchPatientProceduresHandlerTests : ProcedureHandlerTestBase
{
    [Test]
    public async Task Handle_Results_ReturnsThePageAsSummariesWithTotals()
    {
        var query = CreateSearchQuery() with { PageSize = 2 };
        var procedure = CreateProcedure(Status.Assigned);
        Repository
            .Setup(r => r.SearchAsync(query, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PatientProcedure>([procedure], 3));
        var handler = Fixture.Create<SearchPatientProceduresHandler>();

        var result = await handler.Handle(query, CancellationToken.None);

        result.Items.Should().ContainSingle(s => s.Id == procedure.Id && s.Assignees.Single() == Staff.Username);
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
            .ReturnsAsync(new PagedResult<PatientProcedure>([], 0));
        using var cts = new CancellationTokenSource();

        await Fixture.Create<SearchPatientProceduresHandler>().Handle(query, cts.Token);

        Repository.Verify(r => r.SearchAsync(query, Now, cts.Token), Times.Once);
    }
}
