using FluentAssertions;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MedApp.UnitTests.Infrastructure.Data;

/// <summary>
/// Checks that EF Core translates the timeline's query to SQL Server SQL, with every filter and the cursor, without
/// a database: <c>ToQueryString</c> fails if any part would have to run in memory.
/// </summary>
[TestFixture]
public sealed class PatientTimelineTranslationTests
{
    private static readonly Guid PatientId = Guid.Parse("00000021-0000-0000-0000-000000000001");

    private MedAppDbContext _context = null!;

    [SetUp]
    public void SetUp() =>
        _context = new MedAppDbContext(new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer("Server=translation.invalid;Database=MedApp;Trusted_Connection=True")
            .Options);

    [TearDown]
    public void TearDown() => _context.Dispose();

    [TestCase(false)]
    [TestCase(true)]
    public void Page_EveryFilterAndACursor_TranslatesToSql(bool isAdmin)
    {
        var query = new GetPatientTimelineQuery(
            PatientId,
            new TimelineCursor(new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc), Guid.NewGuid()).Encode(),
            20,
            Enum.GetNames<PatientTimelineKind>(),
            [Guid.NewGuid()],
            isAdmin);

        var sql = PatientTimelineSearch
            .Page(_context.PatientProcedureAuditEntries.AsNoTracking(), _context.PatientProcedures.AsNoTracking(), query)
            .ToQueryString();

        sql.Should().Contain("ORDER BY [p].[AtUtc] DESC, [p].[Id] DESC").And.Contain("[p].[Id] < @id").And.Contain("TOP");
        if (isAdmin)
            sql.Should().NotContain("EXISTS");
        else
            sql.Should().Contain("EXISTS");
    }
}
