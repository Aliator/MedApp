using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedUnlistedGpPractices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnlistedPracticeAddressLines",
                table: "Patients",
                type: "nvarchar(302)",
                maxLength: 302,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnlistedPracticeName",
                table: "Patients",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnlistedPracticePhone",
                table: "Patients",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnlistedPracticePostcode",
                table: "Patients",
                type: "varchar(8)",
                unicode: false,
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Patients_OnePractice",
                table: "Patients",
                sql: "[PracticeId] IS NULL OR [UnlistedPracticeName] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Patients_OnePractice",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "UnlistedPracticeAddressLines",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "UnlistedPracticeName",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "UnlistedPracticePhone",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "UnlistedPracticePostcode",
                table: "Patients");
        }
    }
}
