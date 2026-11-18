using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// <c>OverrideStages</c> splits into <c>UnlockStages</c> (unlock a stage, reinstate a skipped one) and
    /// <c>FinishWithoutVisit</c> (finish or skip a stage that requires a visit without one), as decision 138 of
    /// <c>docs/centre-phases-3-5-plan.md</c> says. Roles store permissions by name, so:
    /// <list type="bullet">
    /// <item>every role's <c>OverrideStages</c> row becomes both new names, so no role loses anything (the starting
    /// Clinician role, and any role an admin made or ticked it on);</item>
    /// <item>the starting role <b>Senior clinician</b> is added with a fixed ID: Clinician's starting permissions and
    /// both, unless a role of that name or ID exists already (it's left as it is).</item>
    /// </list>
    /// The built-in Admin role has no rows: its fixed set in code keeps <c>UnlockStages</c> and loses
    /// <c>FinishWithoutVisit</c>, the one deliberate loss. The values are written here, not read from
    /// <c>StandardRoles</c>, so the migration never changes after it ships.
    /// </summary>
    public partial class SplitOverrideStages : Migration
    {
        private const string OldPermission = "OverrideStages";
        private const string UnlockStages = "UnlockStages";
        private const string FinishWithoutVisit = "FinishWithoutVisit";

        private const string SeniorClinicianId = "7a3b0000-0000-4000-8000-000000000008";
        private const string SeniorClinicianName = "Senior clinician";
        private const string SeniorClinicianDescription = "Senior clinicians: as Clinician, and they unlock stages and finish one without its visit.";

        /// <summary>Clinician's starting permissions, with both halves of the old <c>OverrideStages</c>.</summary>
        private static readonly string[] SeniorClinicianPermissions =
        [
            "UseApp", "ViewPatients", "EditPatients", "RegisterReferrals", "TriageReferrals", "CreatePathways", "WorkPathways",
            UnlockStages, FinishWithoutVisit, "BookAppointments", "AttachFiles",
        ];

        private static string Text(string value) => "N'" + value.Replace("'", "''") + "'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var values = string.Join(", ", SeniorClinicianPermissions.Select(p => $"('{p}')"));
            var normalized = Text(SeniorClinicianName.ToUpperInvariant());

            migrationBuilder.Sql($"""
                INSERT INTO RolePermissions (RoleId, Permission)
                SELECT old.RoleId, split.Name
                FROM RolePermissions AS old
                CROSS JOIN (VALUES ('{UnlockStages}'), ('{FinishWithoutVisit}')) AS split(Name)
                WHERE old.Permission = '{OldPermission}'
                  AND NOT EXISTS (SELECT 1 FROM RolePermissions AS rp WHERE rp.RoleId = old.RoleId AND rp.Permission = split.Name);

                DELETE FROM RolePermissions WHERE Permission = '{OldPermission}';

                IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = {normalized} OR Id = '{SeniorClinicianId}')
                BEGIN
                    DECLARE @now datetime2 = SYSUTCDATETIME();
                    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp, Description, IsArchived, IsBuiltIn, CreatedAt, LastUpdated)
                    VALUES ('{SeniorClinicianId}', {Text(SeniorClinicianName)}, {normalized}, LOWER(CONVERT(nvarchar(36), NEWID())), {Text(SeniorClinicianDescription)}, 0, 0, @now, @now);
                    INSERT INTO RolePermissions (RoleId, Permission)
                    SELECT '{SeniorClinicianId}', permission.Name FROM (VALUES {values}) AS permission(Name);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // OverrideStages comes back only on roles that hold both halves, as it allowed both: a role given just one
            // since (UnlockStages alone, say) would otherwise gain the other on the older code. A role with one half
            // loses it there, which refuses rather than allows. Senior clinician stays, with its holders, as an
            // ordinary role (older code needs nothing of it), so going down and up again loses nobody's role.
            migrationBuilder.Sql($"""
                INSERT INTO RolePermissions (RoleId, Permission)
                SELECT unlock.RoleId, '{OldPermission}'
                FROM RolePermissions AS unlock
                WHERE unlock.Permission = '{UnlockStages}'
                  AND EXISTS (SELECT 1 FROM RolePermissions AS finish WHERE finish.RoleId = unlock.RoleId AND finish.Permission = '{FinishWithoutVisit}')
                  AND NOT EXISTS (SELECT 1 FROM RolePermissions AS rp WHERE rp.RoleId = unlock.RoleId AND rp.Permission = '{OldPermission}');

                DELETE FROM RolePermissions WHERE Permission IN ('{UnlockStages}', '{FinishWithoutVisit}');
                """);
        }
    }
}
