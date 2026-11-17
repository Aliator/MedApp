using FluentAssertions;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MedApp.IntegrationTests.Migrations;

/// <summary>
/// <c>AddedProcedureNotes</c> against a real SQL Server: each procedure's old single notes field, if it has any text,
/// becomes its first note, by whoever created it (its Created audit entry) or nobody, with version 1; the old column
/// stays as it was, for API copies from before; and down and up again work. The rows are synthetic, inserted with SQL
/// at the migration before.
/// </summary>
[TestFixture]
public sealed class ProcedureNotesMigrationTests
{
    private const string Before = "20261116214117_AddedMeetings";
    private const string Migration = "20261117081149_AddedProcedureNotes";

    private static readonly Guid Creator = Guid.Parse("0000000f-0000-0000-0000-000000000001");
    private static readonly Guid LaterCreator = Guid.Parse("0000000f-0000-0000-0000-000000000002");
    private static readonly DateTime CreatedAt = new(2026, 3, 4, 10, 30, 0, DateTimeKind.Utc);

    private MedAppDbContext _context = null!;
    private Guid _patientId;

    [SetUp]
    public async Task CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer(TestEnvironment.ServerConnectionString($"MedApp_ProcedureNotes_{Guid.NewGuid():N}"))
            .Options;
        _context = new MedAppDbContext(options);
        await _context.GetService<IMigrator>().MigrateAsync(Before);

        _patientId = Guid.NewGuid();
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Patients (Id, FirstName, LastName, DateOfBirth, Email, CreatedAt, LastUpdated)
            VALUES ({_patientId}, N'Synthetic', N'Person', {new DateTime(1980, 5, 6)}, N'synthetic@example.invalid', {CreatedAt}, {CreatedAt})
            """);
    }

    [TearDown]
    public async Task DropDatabaseAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Test]
    public async Task Up_EachProcedureWithNotes_GetsThemAsItsFirstNote()
    {
        var withCreator = await InsertProcedureAsync("Synthetic notes: **bring** previous scans.\nSecond line.", Creator, "synthetic.creator");
        var noCreator = await InsertProcedureAsync("Imported from the previous system.", null, null);
        var blank = await InsertProcedureAsync(" \r\n\t ", Creator, "synthetic.creator");
        var empty = await InsertProcedureAsync(string.Empty, Creator, "synthetic.creator");
        // Two Created entries (there shouldn't be): the earlier is the author.
        var twice = await InsertProcedureAsync("Synthetic notes, created twice.", Creator, "synthetic.creator");
        await InsertCreatedEntryAsync(twice, LaterCreator, "synthetic.later", CreatedAt.AddMinutes(5));

        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        var notes = await _context.PatientProcedureNotes.AsNoTracking().ToListAsync();
        var versions = await _context.PatientProcedureNoteVersions.AsNoTracking().ToListAsync();
        notes.Select(n => n.PatientProcedureId).Should().BeEquivalentTo([withCreator, noCreator, twice]);
        notes.Should().NotContain(n => n.PatientProcedureId == blank || n.PatientProcedureId == empty);

        var moved = notes.Single(n => n.PatientProcedureId == withCreator);
        moved.Should().BeEquivalentTo(new
        {
            AuthorUserId = (Guid?)Creator,
            Text = "Synthetic notes: **bring** previous scans.\nSecond line.",
            CreatedAtUtc = CreatedAt,
            LastEditedAtUtc = (DateTime?)null,
            Version = 1
        });
        moved.RowVersion.Should().NotBeEmpty();
        notes.Single(n => n.PatientProcedureId == noCreator).AuthorUserId.Should().BeNull();
        notes.Single(n => n.PatientProcedureId == twice).AuthorUserId.Should().Be(Creator);

        versions.Should().HaveCount(3).And.OnlyContain(v => v.Version == 1 && v.PatientId == _patientId && v.AtUtc == CreatedAt);
        versions.Single(v => v.NoteId == moved.Id).Should().BeEquivalentTo(new
        {
            PatientProcedureId = withCreator, moved.Text, UserId = (Guid?)Creator, Username = "synthetic.creator"
        });
        versions.Single(v => v.PatientProcedureId == noCreator).Should().BeEquivalentTo(new { UserId = (Guid?)null, Username = string.Empty });

        // The old column is kept as it was, for API copies from before the move during a rollout.
        (await _context.PatientProcedures.AsNoTracking().SingleAsync(p => p.Id == withCreator)).LegacyNotes.Should().Be(moved.Text);
    }

    [Test]
    public async Task Down_AfterUp_DropsTheNotesAndKeepsTheOldColumn_AndUpAgainWorks()
    {
        var procedure = await InsertProcedureAsync("Synthetic notes.", Creator, "synthetic.creator");
        var migrator = _context.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration);

        await migrator.MigrateAsync(Before);

        var tables = await _context.Database
            .SqlQuery<string>($"SELECT name AS Value FROM sys.tables WHERE name LIKE 'PatientProcedureNote%'")
            .ToListAsync();
        tables.Should().BeEmpty();
        (await _context.Database.SqlQuery<string>($"SELECT Notes AS Value FROM PatientProcedures WHERE Id = {procedure}").SingleAsync())
            .Should().Be("Synthetic notes.");

        await migrator.MigrateAsync(Migration);
        (await _context.PatientProcedureNotes.CountAsync()).Should().Be(1);
    }

    /// <summary>A procedure with these old notes; with a creator, its Created audit entry by them.</summary>
    private async Task<Guid> InsertProcedureAsync(string notes, Guid? creator, string? creatorName)
    {
        var id = Guid.NewGuid();
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO PatientProcedures (Id, Title, Notes, DueDateUtc, Priority, Status, PatientId, TemplateName, CreatedAt, LastUpdated)
            VALUES ({id}, N'Synthetic procedure', {notes}, {CreatedAt.AddDays(30)}, 1, 0, {_patientId}, N'Synthetic template', {CreatedAt}, {CreatedAt})
            """);
        if (creator is { } userId)
            await InsertCreatedEntryAsync(id, userId, creatorName!, CreatedAt);
        return id;
    }

    private Task InsertCreatedEntryAsync(Guid procedureId, Guid userId, string username, DateTime atUtc) =>
        _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO PatientProcedureAuditEntries (Id, PatientProcedureId, ProcedureTitle, PatientId, Action, UserId, Username, AtUtc, Details)
            VALUES ({Guid.NewGuid()}, {procedureId}, N'Synthetic procedure', {_patientId}, 0, {userId}, {username}, {atUtc}, {"{}"})
            """);
}
