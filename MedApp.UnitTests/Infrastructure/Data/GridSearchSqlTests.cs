using FluentAssertions;
using MedApp.Application.DataRequests.Common;
using MedApp.Application.DataRequests.Repositories;
using MedApp.Application.Letters.Queries.SearchLetters;
using MedApp.Application.Results.Queries.SearchResults;
using MedApp.Application.Specimens.Queries.SearchSpecimens;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MedApp.UnitTests.Infrastructure.Data;

/// <summary>
/// The Lab, Results, Letters and Data Requests lists' searches and sorts must translate to SQL Server's SQL, as EF would
/// send them: this builds the SQL without a database (EF only connects to run a query). What they return is covered by
/// the in-memory tests of the same LINQ.
/// </summary>
[TestFixture]
public sealed class GridSearchSqlTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
    private MedAppDbContext _context = null!;

    [SetUp]
    public void SetUp() =>
        _context = new MedAppDbContext(new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer("Server=search.invalid;Database=None")
            .Options);

    [TearDown]
    public void TearDown() => _context.Dispose();

    [TestCase("number")]
    [TestCase("patient")]
    [TestCase("lab")]
    [TestCase("status")]
    [TestCase("time")]
    public void Specimens_SearchAndEachSort_Translate(string sort)
    {
        foreach (var view in Enum.GetNames<SpecimenView>())
        {
            var query = new SearchSpecimensQuery(1, 10, view, null, null, null, "MA26 000123", sort, false);

            var sql = _context.Specimens.Filter(query, Now).Sort(query.ParsedView, query.Sort, query.Ascending).ToQueryString();

            sql.Should().Contain("LIKE").And.Contain("[NhsNumber]").And.Contain("ORDER BY");
        }
    }

    [TestCase("patient")]
    [TestCase("title")]
    [TestCase("pathway")]
    [TestCase("reported")]
    [TestCase("status")]
    public void Results_SearchAndEachSort_Translate(string sort)
    {
        var query = new SearchResultsQuery(1, 10, "All", null, null, null, Guid.NewGuid(), "freya", sort, true);

        var sql = _context.Results.Filter(query, [Guid.NewGuid()]).Sort(query.ParsedView, query.Sort, query.Ascending).ToQueryString();

        sql.Should().Contain("LIKE").And.Contain("ORDER BY");
    }

    [TestCase("patient")]
    [TestCase("letter")]
    [TestCase("status")]
    [TestCase("latest")]
    public void Letters_SearchAndEachSort_Translate(string sort)
    {
        var query = new SearchLettersQuery(1, 10, "All", null, null, null, Guid.NewGuid(), "freya", sort, false);

        var sql = LetterSearch.Apply(_context.Letters, query, _context.Patients).ToQueryString();

        sql.Should().Contain("EXISTS").And.Contain("LIKE").And.Contain("ORDER BY");
        if (sort == "patient")
            sql.Should().Contain("SELECT TOP(1) [p");
        if (sort == "latest")
            sql.Should().Contain("COALESCE").And.Contain("CASE");
    }

    [TestCase("patient")]
    [TestCase("received")]
    [TestCase("due")]
    [TestCase("status")]
    public void DataRequests_SearchAndEachSort_Translate(string sort)
    {
        var filter = new DataRequestFilter(null, null, false, "freya", sort, false);

        var sql = DataRequestSearch.Order(DataRequestSearch.Apply(_context.DataRequests, filter), filter).ToQueryString();

        sql.Should().Contain("LIKE").And.Contain("ORDER BY");
    }
}
