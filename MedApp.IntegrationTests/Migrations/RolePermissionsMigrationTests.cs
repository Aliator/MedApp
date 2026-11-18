using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MedApp.IntegrationTests.Migrations;

/// <summary>
/// <c>AddedRolePermissions</c> against a real SQL Server, from a database with the old roles: Admin becomes the
/// built-in role, User becomes "All staff (before roles)" with exactly the staff permissions of before, keeping its ID
/// and members, the starting roles are added (unless a role has the name already), and a role an admin made stays
/// with no permissions. Down puts the old roles back. Rows are synthetic and written with SQL at the migration before.
/// </summary>
[TestFixture]
public sealed class RolePermissionsMigrationTests
{
    private const string Before = "20261118075440_RenamedProceduresToPathways";
    private const string Migration = "20261118111735_AddedRolePermissions";
    private const string BeforeWorkingHours = "20261118131343_AddedReferrals";
    private const string WorkingHoursMigration = "20261118133740_AddedWorkingHours";
    private const string SpecimensMigration = "20261118175100_AddedSpecimens";

    /// <summary>The permissions later migrations add (AddedSpecimens), which the starting roles have now.</summary>
    private static readonly Permission[] SpecimenPermissions = [Permission.RecordSpecimens, Permission.ProcessSpecimens];

    private static readonly Guid AdminRoleId = Guid.Parse("0000000d-0000-0000-0000-000000000001");
    private static readonly Guid UserRoleId = Guid.Parse("0000000d-0000-0000-0000-000000000002");

    /// <summary>A role an admin made before, which happens to share a starting role's name.</summary>
    private static readonly Guid MadeClinicianRoleId = Guid.Parse("0000000d-0000-0000-0000-000000000003");

    private static readonly Guid AdminUserId = Guid.Parse("0000000e-0000-0000-0000-000000000001");
    private static readonly Guid StaffUserId = Guid.Parse("0000000e-0000-0000-0000-000000000002");

    private MedAppDbContext _context = null!;

    [SetUp]
    public async Task CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer(TestEnvironment.ServerConnectionString($"MedApp_RolePermissions_{Guid.NewGuid():N}"))
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
    public async Task Up_OldRoles_BecomeTheBuiltInAdminAndAllStaff_WithTheirMembers()
    {
        await SeedOldRolesAsync();

        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        var roles = await RolesAsync();
        roles[AdminRoleId].Should().Be((AppRoles.Admin, "ADMIN", true, false));
        roles[UserRoleId].Should().Be((AppRoles.AllStaffBeforeRoles, "ALL STAFF (BEFORE ROLES)", false, false));
        (await PermissionsOfAsync(AdminRoleId)).Should().BeEmpty("Admin's permissions are fixed in code");
        (await PermissionsOfAsync(UserRoleId)).Should().BeEquivalentTo(
            StandardRoles.AllStaffBeforeRoles.Permissions.Select(p => p.ToString()));

        (await MembershipsAsync()).Should().BeEquivalentTo(
            [(AdminUserId, AdminRoleId), (AdminUserId, UserRoleId), (StaffUserId, UserRoleId), (StaffUserId, MadeClinicianRoleId)]);
    }

    [Test]
    public async Task Up_AddsTheStartingRoles_ButLeavesARoleAnAdminMadeWithTheSameName()
    {
        await SeedOldRolesAsync();

        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        var roles = await RolesAsync();
        roles.Should().NotContainKey(StandardRoles.Clinician.Id);
        roles[MadeClinicianRoleId].Should().Be(("Clinician", "CLINICIAN", false, false));
        (await PermissionsOfAsync(MadeClinicianRoleId)).Should().BeEmpty();

        foreach (var role in StandardRoles.Starting.Where(r => r != StandardRoles.Clinician && r != StandardRoles.SeniorClinician))
        {
            roles[role.Id].Should().Be((role.Name, role.Name.ToUpperInvariant(), false, false));
            // ManageClinics came later, with AddedWorkingHours, and the specimen permissions with AddedSpecimens.
            (await PermissionsOfAsync(role.Id)).Should().BeEquivalentTo(
                role.Permissions.Where(p => p != Permission.ManageClinics && !SpecimenPermissions.Contains(p)).Select(p => p.ToString()), role.Name);
        }
    }

    [Test]
    public async Task WorkingHours_UpGivesManageClinicsToBookingAndManager_AndDownTakesItAway()
    {
        await SeedOldRolesAsync();
        await _context.GetService<IMigrator>().MigrateAsync(WorkingHoursMigration);

        foreach (var role in StandardRoles.Starting.Where(r => r != StandardRoles.Clinician && r != StandardRoles.SeniorClinician))
            (await PermissionsOfAsync(role.Id)).Should().BeEquivalentTo(
                role.Permissions.Where(p => !SpecimenPermissions.Contains(p)).Select(p => p.ToString()), role.Name);
        (await PermissionsOfAsync(MadeClinicianRoleId)).Should().BeEmpty("a role an admin made with a starting role's name is left alone");

        await _context.GetService<IMigrator>().MigrateAsync(BeforeWorkingHours);

        (await PermissionsOfAsync(StandardRoles.Booking.Id)).Should().NotContain(nameof(Permission.ManageClinics));
        (await PermissionsOfAsync(StandardRoles.Manager.Id)).Should().NotContain(nameof(Permission.ManageClinics));
    }

    [Test]
    public async Task Specimens_UpGivesTheSpecimenPermissionsToTheStartingRoles_AndDownTakesThemAway()
    {
        await _context.GetService<IMigrator>().MigrateAsync(SpecimensMigration);

        foreach (var role in StandardRoles.Starting)
            (await PermissionsOfAsync(role.Id)).Should().BeEquivalentTo(role.Permissions.Select(p => p.ToString()), role.Name);
        (await PermissionsOfAsync(StandardRoles.Admin.Id)).Should().BeEmpty("Admin's permissions are fixed in code, without these");

        await _context.GetService<IMigrator>().MigrateAsync(WorkingHoursMigration);

        (await _context.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM RolePermissions WHERE Permission LIKE '%Specimens'").SingleAsync())
            .Should().Be(0);
    }

    [Test]
    public async Task Up_NewDatabase_MakesEveryStandardRoleWithItsFixedId()
    {
        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        var roles = await RolesAsync();
        // Senior clinician came later, with SplitOverrideStages.
        roles.Keys.Should().BeEquivalentTo(StandardRoles.All.Where(r => r != StandardRoles.SeniorClinician).Select(r => r.Id));
        roles[StandardRoles.Admin.Id].IsBuiltIn.Should().BeTrue();
        (await PermissionsOfAsync(StandardRoles.Clinician.Id)).Should().Contain(nameof(Permission.TriageReferrals));
    }

    [Test]
    public async Task Down_PutsTheOldRolesBack_WithTheirMembers()
    {
        await SeedOldRolesAsync();
        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        await _context.GetService<IMigrator>().MigrateAsync(Before);

        var roles = await _context.Database
            .SqlQuery<string>($"SELECT Name AS Value FROM AspNetRoles")
            .ToListAsync();
        roles.Should().BeEquivalentTo([AppRoles.Admin, "User", "Clinician"]);
        (await _context.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM AspNetUserRoles").SingleAsync()).Should().Be(4);
    }

    private async Task SeedOldRolesAsync()
    {
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) VALUES
                ({AdminRoleId}, N'Admin', N'ADMIN', N'stamp-1'),
                ({UserRoleId}, N'User', N'USER', N'stamp-2'),
                ({MadeClinicianRoleId}, N'Clinician', N'CLINICIAN', N'stamp-3');
            INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, CreatedAt, MustChangePassword, TwoFactorLockouts, TwoFactorMethod) VALUES
                ({AdminUserId}, N'synthetic.admin', N'SYNTHETIC.ADMIN', 0, 0, 0, 1, 0, {new DateTime(2026, 1, 5, 9, 0, 0)}, 0, 0, 0),
                ({StaffUserId}, N'synthetic.staff', N'SYNTHETIC.STAFF', 0, 0, 0, 1, 0, {new DateTime(2026, 1, 5, 9, 0, 0)}, 0, 0, 0);
            INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES
                ({AdminUserId}, {AdminRoleId}), ({AdminUserId}, {UserRoleId}), ({StaffUserId}, {UserRoleId}), ({StaffUserId}, {MadeClinicianRoleId});
            """);
    }

    private async Task<Dictionary<Guid, (string Name, string NormalizedName, bool IsBuiltIn, bool IsArchived)>> RolesAsync()
    {
        var rows = await _context.Roles.AsNoTracking()
            .Select(r => new { r.Id, r.Name, r.NormalizedName, r.IsBuiltIn, r.IsArchived })
            .ToListAsync();
        return rows.ToDictionary(r => r.Id, r => (r.Name!, r.NormalizedName!, r.IsBuiltIn, r.IsArchived));
    }

    private Task<List<string>> PermissionsOfAsync(Guid roleId) =>
        _context.RolePermissions.AsNoTracking().Where(p => p.RoleId == roleId).Select(p => p.Permission).ToListAsync();

    private async Task<List<(Guid UserId, Guid RoleId)>> MembershipsAsync() =>
        (await _context.UserRoles.AsNoTracking().Select(r => new { r.UserId, r.RoleId }).ToListAsync())
        .Select(r => (r.UserId, r.RoleId))
        .ToList();
}
