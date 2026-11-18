using FluentAssertions;
using MedApp.Domain.Teams;
using MedApp.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MedApp.IntegrationTests.Migrations;

/// <summary>
/// <c>AddedSpecialties</c> against a real SQL Server: General is added, every existing template and pathway goes into
/// it, and a template or pathway inserted as an API copy from before specialties would (without the column) still goes
/// into it; down takes it all away again and keeps the rows. The rows are synthetic, inserted with SQL at the migration
/// before, and read with SQL, as a later migration may add columns to these tables (AGENTS.md, Migrations).
/// </summary>
[TestFixture]
public sealed class SpecialtiesMigrationTests
{
    private const string Before = "20261118111735_AddedRolePermissions";
    private const string Migration = "20261118120357_AddedSpecialties";

    private static readonly Guid Patient = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000005a");
    private static readonly Guid Template = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000005a");
    private static readonly Guid Pathway = Guid.Parse("cccccccc-0000-0000-0000-00000000005a");
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    private MedAppDbContext _context = null!;

    [SetUp]
    public async Task CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer(TestEnvironment.ServerConnectionString($"MedApp_Specialties_{Guid.NewGuid():N}"))
            .Options;
        _context = new MedAppDbContext(options);
        await _context.GetService<IMigrator>().MigrateAsync(Before);
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Patients (Id, FirstName, LastName, DateOfBirth, CreatedAt, LastUpdated) VALUES
                ({Patient}, N'Synthetic', N'Person', '1980-05-06', {Now}, {Now});
            INSERT INTO PathwayTemplates (Id, Name) VALUES ({Template}, N'Synthetic template');
            INSERT INTO PatientPathways (Id, Title, Notes, DueDateUtc, Priority, Status, PatientId, TemplateId, TemplateName, CreatedAt, LastUpdated) VALUES
                ({Pathway}, N'Synthetic pathway', N'', {Now}, 1, 1, {Patient}, {Template}, N'Synthetic template', {Now}, {Now});
            """);
    }

    [TearDown]
    public async Task DropDatabaseAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Test]
    public async Task Up_ExistingTemplatesAndPathways_GoIntoGeneral()
    {
        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        (await TextAsync($"SELECT Name AS Value FROM Specialties WHERE Id = {Team.GeneralId} AND IsArchived = 0"))
            .Should().Be(Team.GeneralName);
        (await GuidAsync($"SELECT SpecialtyId AS Value FROM PathwayTemplates WHERE Id = {Template}")).Should().Be(Team.GeneralId);
        (await GuidAsync($"SELECT SpecialtyId AS Value FROM PatientPathways WHERE Id = {Pathway}")).Should().Be(Team.GeneralId);
    }

    [Test]
    public async Task Up_AnInsertThatDoesntKnowSpecialties_StillGoesIntoGeneral()
    {
        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        // As an API copy from before this release inserts, during a rollout: no SpecialtyId column at all.
        var oldTemplate = Guid.NewGuid();
        var oldPathway = Guid.NewGuid();
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO PathwayTemplates (Id, Name) VALUES ({oldTemplate}, N'Template from an old copy');
            INSERT INTO PatientPathways (Id, Title, Notes, DueDateUtc, Priority, Status, PatientId, TemplateId, TemplateName, CreatedAt, LastUpdated) VALUES
                ({oldPathway}, N'Pathway from an old copy', N'', {Now}, 1, 1, {Patient}, {oldTemplate}, N'Template from an old copy', {Now}, {Now});
            """);

        (await GuidAsync($"SELECT SpecialtyId AS Value FROM PathwayTemplates WHERE Id = {oldTemplate}")).Should().Be(Team.GeneralId);
        (await GuidAsync($"SELECT SpecialtyId AS Value FROM PatientPathways WHERE Id = {oldPathway}")).Should().Be(Team.GeneralId);
    }

    [Test]
    public async Task Up_ASpecialtyInUse_CantBeDeleted()
    {
        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        var act = () => _context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Specialties WHERE Id = {Team.GeneralId}");

        await act.Should().ThrowAsync<SqlException>();
    }

    [Test]
    public async Task Down_RemovesSpecialties_KeepsTheRows_AndUpAgainWorks()
    {
        var migrator = _context.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration);

        await migrator.MigrateAsync(Before);

        (await CountAsync($"SELECT COUNT(*) AS Value FROM sys.tables WHERE name IN (N'Specialties', N'StaffSpecialties')")).Should().Be(0);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM sys.columns WHERE name = N'SpecialtyId'")).Should().Be(0);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathways WHERE Id = {Pathway} AND TemplateId = {Template}")).Should().Be(1);

        await migrator.MigrateAsync(Migration);
        (await GuidAsync($"SELECT SpecialtyId AS Value FROM PatientPathways WHERE Id = {Pathway}")).Should().Be(Team.GeneralId);
    }

    private Task<string> TextAsync(FormattableString sql) => _context.Database.SqlQuery<string>(sql).SingleAsync();

    private Task<Guid> GuidAsync(FormattableString sql) => _context.Database.SqlQuery<Guid>(sql).SingleAsync();

    private Task<int> CountAsync(FormattableString sql) => _context.Database.SqlQuery<int>(sql).SingleAsync();
}
