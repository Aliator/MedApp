using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MedApp.IntegrationTests.Migrations;

/// <summary>
/// <c>SplitOverrideStages</c> against a real SQL Server (decision 138 of <c>docs/centre-phases-3-5-plan.md</c>): every
/// role's <c>OverrideStages</c> row becomes <c>UnlockStages</c> and <c>FinishWithoutVisit</c>, so no role loses
/// anything; the starting role Senior clinician is added with its fixed ID, unless a role has its name already. Down
/// gives <c>OverrideStages</c> back only to roles holding both halves. Rows are synthetic and written with SQL at the
/// migration before.
/// </summary>
[TestFixture]
public sealed class SplitOverrideStagesMigrationTests
{
    private const string Before = "20261118171158_AddedPatientMessages";
    private const string Migration = "20261118173821_SplitOverrideStages";

    /// <summary>A role an admin made, with OverrideStages ticked.</summary>
    private static readonly Guid LocumRoleId = Guid.Parse("0000000d-0000-0000-0000-000000000101");

    private MedAppDbContext _context = null!;

    [SetUp]
    public async Task CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer(TestEnvironment.ServerConnectionString($"MedApp_SplitOverrideStages_{Guid.NewGuid():N}"))
            .Options;
        _context = new MedAppDbContext(options);
        await _context.GetService<IMigrator>().MigrateAsync(Before);
    }

    [TearDown]
    public async Task DropDatabaseAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Test]
    public async Task Up_EveryOverrideStagesRow_BecomesBothHalves_AndNoRoleLosesAnything()
    {
        await AddRoleAsync(LocumRoleId, "Locum", "OverrideStages", "WorkPathways");
        var clinicianBefore = await PermissionsOfAsync(StandardRoles.Clinician.Id);
        clinicianBefore.Should().Contain("OverrideStages");

        await MigrateAsync(Migration);

        (await PermissionsOfAsync(StandardRoles.Clinician.Id)).Should().BeEquivalentTo(
            clinicianBefore.Where(p => p != "OverrideStages").Append(nameof(Permission.UnlockStages)).Append(nameof(Permission.FinishWithoutVisit)));
        (await PermissionsOfAsync(LocumRoleId)).Should().BeEquivalentTo(
            [nameof(Permission.WorkPathways), nameof(Permission.UnlockStages), nameof(Permission.FinishWithoutVisit)]);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM RolePermissions WHERE Permission = 'OverrideStages'")).Should().Be(0);
    }

    /// <summary>A new database ends up with exactly the standard roles, as <c>StandardRoles</c> has them.</summary>
    [Test]
    public async Task Up_AddsSeniorClinician_SoEveryStandardRoleIsAsStandardRolesHasIt()
    {
        await MigrateAsync(Migration);

        var roles = await _context.Roles.AsNoTracking().Select(r => new { r.Id, r.Name, r.Description, r.IsBuiltIn, r.IsArchived }).ToListAsync();
        roles.Select(r => r.Id).Should().BeEquivalentTo(LaterMigrationPermissions.AllAsAt(Migration).Select(r => r.Id));
        // Its description as this migration wrote it (AddedMultiClinicCentre reworded it).
        roles.Single(r => r.Id == StandardRoles.SeniorClinician.Id).Should().BeEquivalentTo(new
        {
            StandardRoles.SeniorClinician.Name,
            Description = "Senior clinicians: as Clinician, and they unlock stages and finish one without its visit.",
            IsBuiltIn = false,
            IsArchived = false,
        });
        foreach (var role in LaterMigrationPermissions.StartingAsAt(Migration))
            (await PermissionsOfAsync(role.Id)).Should().BeEquivalentTo(LaterMigrationPermissions.AsAt(role, Migration), role.Name);
    }

    [Test]
    public async Task Up_ARoleAlreadyNamedSeniorClinician_IsLeftAsItIs()
    {
        var madeId = Guid.Parse("0000000d-0000-0000-0000-000000000102");
        await AddRoleAsync(madeId, "Senior Clinician", "ViewPatients");

        await MigrateAsync(Migration);

        (await _context.Roles.AsNoTracking().AnyAsync(r => r.Id == StandardRoles.SeniorClinician.Id)).Should().BeFalse();
        (await PermissionsOfAsync(madeId)).Should().Equal(nameof(Permission.ViewPatients));
    }

    [Test]
    public async Task Down_GivesOverrideStagesBackOnlyToRolesHoldingBothHalves_AndKeepsSeniorClinician()
    {
        await MigrateAsync(Migration);
        var unlockOnlyId = Guid.Parse("0000000d-0000-0000-0000-000000000103");
        await AddRoleAsync(unlockOnlyId, "Unlockers", "UnlockStages", "ViewPatients");

        await MigrateAsync(Before);

        (await PermissionsOfAsync(StandardRoles.Clinician.Id)).Should().Contain("OverrideStages")
            .And.NotContain(nameof(Permission.UnlockStages)).And.NotContain(nameof(Permission.FinishWithoutVisit));
        (await PermissionsOfAsync(StandardRoles.SeniorClinician.Id)).Should().Contain("OverrideStages");
        (await PermissionsOfAsync(unlockOnlyId)).Should().Equal(nameof(Permission.ViewPatients));
    }

    private Task MigrateAsync(string migration) => _context.GetService<IMigrator>().MigrateAsync(migration);

    private Task AddRoleAsync(Guid id, string name, params string[] permissions) =>
        _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp, Description, IsArchived, IsBuiltIn, CreatedAt, LastUpdated) " +
            "VALUES ({0}, {1}, {2}, N'stamp', N'Synthetic role.', 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME());" +
            string.Concat(permissions.Select((_, i) => $" INSERT INTO RolePermissions (RoleId, Permission) VALUES ({{0}}, {{{i + 3}}});")),
            new object[] { id, name, name.ToUpperInvariant() }.Concat(permissions).ToArray());

    private Task<int> CountAsync(FormattableString sql) => _context.Database.SqlQuery<int>(sql).SingleAsync();

    private Task<List<string>> PermissionsOfAsync(Guid roleId) =>
        _context.RolePermissions.AsNoTracking().Where(p => p.RoleId == roleId).Select(p => p.Permission).ToListAsync();
}
