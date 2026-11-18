using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// Roles become data with permissions (docs/centre-phase2-plan.md, decisions 51 to 68): <c>AspNetRoles</c> gains a
    /// description, archived and built-in flags and timestamps, and <c>RolePermissions</c> holds each role's permissions
    /// by name. Then the data, so nobody gains or loses anything on deploy:
    /// <list type="bullet">
    /// <item><c>Admin</c> becomes the built-in role (its permissions are fixed in code, so it has no rows); a database
    /// without one gets it, with a fixed ID.</item>
    /// <item><c>User</c> is renamed "All staff (before roles)" and given exactly what staff could do before roles; a
    /// database without it gets it.</item>
    /// <item>The five starting roles (Clinician, Nurse, Booking, Lab, Manager) are added with fixed IDs and their
    /// permissions, unless a role of that name exists already (it's left as it is, with no permissions).</item>
    /// <item>Any other role stays, with no permissions (every role still grants UseApp).</item>
    /// </list>
    /// The values are written here, not read from <c>StandardRoles</c>, so the migration never changes after it ships.
    /// </summary>
    public partial class AddedRolePermissions : Migration
    {
        private const string AdminId = "7a3b0000-0000-4000-8000-000000000001";
        private const string AllStaffId = "7a3b0000-0000-4000-8000-000000000002";
        private const string AllStaffName = "All staff (before roles)";
        private const string AdminDescription = "Runs MedApp: accounts, roles, setup and privacy. Built in, with fixed permissions.";
        private const string AllStaffDescription = "What every member of staff could do before roles. Move people onto the starting roles, then archive it.";

        private static readonly (string Id, string Name, string Description, string[] Permissions)[] StartingRoles =
        [
            ("7a3b0000-0000-4000-8000-000000000003", "Clinician", "Clinicians who triage referrals and lead pathways.",
                ["UseApp", "ViewPatients", "EditPatients", "RegisterReferrals", "TriageReferrals", "CreatePathways", "WorkPathways", "OverrideStages", "BookAppointments", "AttachFiles"]),
            ("7a3b0000-0000-4000-8000-000000000004", "Nurse", "Nurses and healthcare assistants who work on pathways.",
                ["UseApp", "ViewPatients", "EditPatients", "RegisterReferrals", "CreatePathways", "WorkPathways", "BookAppointments", "AttachFiles"]),
            ("7a3b0000-0000-4000-8000-000000000005", "Booking", "The booking team: patients, referrals, appointments and calendars.",
                ["UseApp", "ViewPatients", "EditPatients", "DeletePatients", "RegisterReferrals", "BookAppointments", "ManageAnyCalendar", "AttachFiles"]),
            ("7a3b0000-0000-4000-8000-000000000006", "Lab", "Laboratory staff who work on their pathway stages.",
                ["UseApp", "ViewPatients", "WorkPathways", "AttachFiles"]),
            ("7a3b0000-0000-4000-8000-000000000007", "Manager", "Service managers: assigning work, meetings, Insights and monitoring.",
                ["UseApp", "ViewPatients", "AssignPathways", "ArrangeMeetings", "ViewInsights", "ManageMonitoring"]),
        ];

        /// <summary>Exactly what the Staff policy allowed: every endpoint that was <c>AuthPolicies.Staff</c>.</summary>
        private static readonly string[] AllStaffPermissions =
            ["UseApp", "ViewPatients", "EditPatients", "DeletePatients", "RegisterReferrals", "CreatePathways", "WorkPathways", "BookAppointments", "AttachFiles"];

        private static string Values(string[] permissions) =>
            string.Join(", ", permissions.Select(p => $"('{p}')"));

        private static string Text(string value) => "N'" + value.Replace("'", "''") + "'";

        private const string NewStamp = "LOWER(CONVERT(nvarchar(36), NEWID()))";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetRoles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AspNetRoles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "AspNetRoles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsBuiltIn",
                table: "AspNetRoles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdated",
                table: "AspNetRoles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permission = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.Permission });
                    table.ForeignKey(
                        name: "FK_RolePermissions_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            var allStaffNormalized = Text(AllStaffName.ToUpperInvariant());
            var sql = new System.Text.StringBuilder();
            sql.AppendLine("DECLARE @now datetime2 = SYSUTCDATETIME();");
            sql.AppendLine("UPDATE AspNetRoles SET CreatedAt = @now, LastUpdated = @now;");

            // Admin: built in, with its permissions in code.
            sql.AppendLine("IF EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = N'ADMIN')");
            sql.AppendLine($"    UPDATE AspNetRoles SET IsBuiltIn = 1, Description = {Text(AdminDescription)} WHERE NormalizedName = N'ADMIN';");
            sql.AppendLine("ELSE");
            sql.AppendLine("    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp, Description, IsArchived, IsBuiltIn, CreatedAt, LastUpdated)");
            sql.AppendLine($"    VALUES ('{AdminId}', N'Admin', N'ADMIN', {NewStamp}, {Text(AdminDescription)}, 0, 1, @now, @now);");

            // User: renamed, keeping its ID and its members, with exactly the staff permissions of before.
            sql.AppendLine("IF EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = N'USER')");
            sql.AppendLine($"    UPDATE AspNetRoles SET Name = {Text(AllStaffName)}, NormalizedName = {allStaffNormalized}, ConcurrencyStamp = {NewStamp}, Description = {Text(AllStaffDescription)} WHERE NormalizedName = N'USER';");
            sql.AppendLine($"ELSE IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = {allStaffNormalized})");
            sql.AppendLine("    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp, Description, IsArchived, IsBuiltIn, CreatedAt, LastUpdated)");
            sql.AppendLine($"    VALUES ('{AllStaffId}', {Text(AllStaffName)}, {allStaffNormalized}, {NewStamp}, {Text(AllStaffDescription)}, 0, 0, @now, @now);");
            sql.AppendLine("INSERT INTO RolePermissions (RoleId, Permission)");
            sql.AppendLine($"SELECT role.Id, permission.Name FROM AspNetRoles AS role CROSS JOIN (VALUES {Values(AllStaffPermissions)}) AS permission(Name)");
            sql.AppendLine($"WHERE role.NormalizedName = {allStaffNormalized};");

            // The starting roles, unless an admin already made a role with one of their names (left as it was).
            foreach (var (id, name, description, permissions) in StartingRoles)
            {
                sql.AppendLine($"IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = {Text(name.ToUpperInvariant())} OR Id = '{id}')");
                sql.AppendLine("BEGIN");
                sql.AppendLine("    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp, Description, IsArchived, IsBuiltIn, CreatedAt, LastUpdated)");
                sql.AppendLine($"    VALUES ('{id}', {Text(name)}, {Text(name.ToUpperInvariant())}, {NewStamp}, {Text(description)}, 0, 0, @now, @now);");
                sql.AppendLine("    INSERT INTO RolePermissions (RoleId, Permission)");
                sql.AppendLine($"    SELECT '{id}', permission.Name FROM (VALUES {Values(permissions)}) AS permission(Name);");
                sql.AppendLine("END;");
            }

            migrationBuilder.Sql(sql.ToString());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The starting roles go (with who held them); All staff is User again. Admin stays Admin.
            var startingIds = string.Join(", ", StartingRoles.Select(role => $"'{role.Id}'"));
            migrationBuilder.Sql(
                $"DELETE FROM AspNetRoles WHERE Id IN ({startingIds});\n"
                + $"UPDATE AspNetRoles SET Name = N'User', NormalizedName = N'USER', ConcurrencyStamp = {NewStamp} "
                + $"WHERE NormalizedName = {Text(AllStaffName.ToUpperInvariant())};");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "IsBuiltIn",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "LastUpdated",
                table: "AspNetRoles");
        }
    }
}
