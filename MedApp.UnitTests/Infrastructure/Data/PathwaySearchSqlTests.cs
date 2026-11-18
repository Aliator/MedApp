using FluentAssertions;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Infrastructure.Data;

/// <summary>
/// The pathway search's newer filters must translate to SQL Server's SQL, as EF would send them: this builds the SQL
/// without a database (EF only connects to run a query). What they return is covered by the in-memory tests of the
/// same LINQ (<c>PathwaySearchTests</c>).
/// </summary>
[TestFixture]
public sealed class PathwaySearchSqlTests
{
    private MedAppDbContext _context = null!;

    [SetUp]
    public void SetUp() =>
        _context = new MedAppDbContext(new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer("Server=search.invalid;Database=None")
            .Options);

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public void Filter_StageReached_TranslatesTheCurrentStageToAnOrderedTopOneSubquery()
    {
        var query = CreateSearchQuery() with { StageDefinitionIds = [PathwayDefinitionId, ConsultationDefinitionId] };

        var sql = _context.PatientPathways.Filter(query, Now).Sort(null, true).ToQueryString();

        sql.Should().Contain("SELECT TOP(1)").And.Contain("[StageDefinitionId]").And.Contain("ORDER BY [p0].[StageOrder]")
            .And.Contain("[IsCompleted] = CAST(0 AS bit)");
    }

    [Test]
    public void Filter_Specialties_TranslatesToAnInList()
    {
        var query = CreateSearchQuery() with { SpecialtyIds = [SpecialtyId, Guid.NewGuid()] };

        _context.PatientPathways.Filter(query, Now).ToQueryString().Should().Contain("[SpecialtyId] IN (");
    }
}
