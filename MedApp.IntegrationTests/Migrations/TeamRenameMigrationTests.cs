using FluentAssertions;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MedApp.IntegrationTests.Migrations;

/// <summary>
/// <c>RenamedSpecialtiesToTeams</c> against a real SQL Server: a specialty, a membership and a template in it are kept
/// under the new table and column names, with the keys and indexes renamed, and General stays the templates' default;
/// down puts the old names back, and up again works. The rows are synthetic, inserted with SQL at the migration before,
/// by the old names, and read with SQL (AGENTS.md, Migrations).
/// </summary>
[TestFixture]
public sealed class TeamRenameMigrationTests
{
    private const string Before = "20261118205031_AddedUnlistedGpPractices";
    private const string Migration = "20261118233909_RenamedSpecialtiesToTeams";

    private static readonly Guid Team = Guid.Parse("5a5a5a5a-0000-0000-0000-000000000001");
    private static readonly Guid User = Guid.Parse("5b5b5b5b-0000-0000-0000-000000000001");
    private static readonly Guid Template = Guid.Parse("5c5c5c5c-0000-0000-0000-000000000001");
    private static readonly Guid DefaultTemplate = Guid.Parse("5c5c5c5c-0000-0000-0000-000000000002");
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    private MedAppDbContext _context = null!;

    [SetUp]
    public async Task CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer(TestEnvironment.ServerConnectionString($"MedApp_TeamRename_{Guid.NewGuid():N}"))
            .Options;
        _context = new MedAppDbContext(options);
        await _context.GetService<IMigrator>().MigrateAsync(Before);
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Specialties (Id, Name, IsArchived, Version, TargetDaysLow, TargetDaysMedium, TargetDaysHigh, TargetDaysUrgent, CreatedAt, LastUpdated)
                VALUES ({Team}, N'Synthetic team', 0, 3, 100, 90, 20, 7, {Now}, {Now});
            INSERT INTO AspNetUsers (Id, UserName, CreatedAt, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
                VALUES ({User}, N'synthetic.member', {Now}, 0, 0, 0, 0, 0);
            INSERT INTO StaffSpecialties (UserId, SpecialtyId) VALUES ({User}, {Team});
            INSERT INTO PathwayTemplates (Id, Name, SpecialtyId) VALUES ({Template}, N'Synthetic template', {Team});
            """);
    }

    [TearDown]
    public async Task DropDatabaseAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Test]
    public async Task Up_EveryRow_IsKeptUnderTheNewNames()
    {
        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        (await CountAsync($"SELECT COUNT(*) AS Value FROM Teams WHERE Id = {Team} AND Name = N'Synthetic team' AND Version = 3 AND TargetDaysUrgent = 7")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM TeamMemberships WHERE UserId = {User} AND TeamId = {Team}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PathwayTemplates WHERE Id = {Template} AND TeamId = {Team}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM sys.columns WHERE name LIKE N'%Specialt%'")).Should().Be(0);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM sys.objects WHERE name IN (N'PK_Teams', N'PK_TeamMemberships', N'FK_TeamMemberships_Teams_TeamId', N'FK_TeamMemberships_AspNetUsers_UserId', N'FK_PathwayTemplates_Teams_TeamId')")).Should().Be(5);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM sys.indexes WHERE name IN (N'IX_Teams_Name', N'IX_TeamMemberships_TeamId', N'IX_PathwayTemplates_TeamId')")).Should().Be(3);

        // An insert that leaves the team out still goes into General, as before.
        await _context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO PathwayTemplates (Id, Name) VALUES ({DefaultTemplate}, N'Synthetic default')");
        (await GuidAsync($"SELECT TeamId AS Value FROM PathwayTemplates WHERE Id = {DefaultTemplate}")).Should().Be(Domain.Teams.Team.GeneralId);
    }

    [Test]
    public async Task Down_PutsTheOldNamesBack_KeepsTheRows_AndUpAgainWorks()
    {
        var migrator = _context.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration);

        await migrator.MigrateAsync(Before);

        (await CountAsync($"SELECT COUNT(*) AS Value FROM sys.tables WHERE name IN (N'Teams', N'TeamMemberships')")).Should().Be(0);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM StaffSpecialties WHERE UserId = {User} AND SpecialtyId = {Team}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PathwayTemplates WHERE Id = {Template} AND SpecialtyId = {Team}")).Should().Be(1);

        await migrator.MigrateAsync(Migration);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM TeamMemberships WHERE UserId = {User} AND TeamId = {Team}")).Should().Be(1);
    }

    private Task<Guid> GuidAsync(FormattableString sql) => _context.Database.SqlQuery<Guid>(sql).SingleAsync();

    private Task<int> CountAsync(FormattableString sql) => _context.Database.SqlQuery<int>(sql).SingleAsync();
}
