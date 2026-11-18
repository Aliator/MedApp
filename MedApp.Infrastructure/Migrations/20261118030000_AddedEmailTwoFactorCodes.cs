using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedEmailTwoFactorCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TwoFactorMethod",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Every account with two-factor on until now uses an authenticator app.
            migrationBuilder.Sql("UPDATE [AspNetUsers] SET [TwoFactorMethod] = 1 WHERE [TwoFactorEnabled] = 1;");

            migrationBuilder.CreateTable(
                name: "EmailCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Resends = table.Column<int>(type: "int", nullable: false),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailCodes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailCodes_SignInChallenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "SignInChallenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCodes_ChallengeId",
                table: "EmailCodes",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCodes_ExpiresAtUtc",
                table: "EmailCodes",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCodes_UserId_Purpose",
                table: "EmailCodes",
                columns: new[] { "UserId", "Purpose" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Accounts using codes by email keep two-factor on, and after this would be asked for an authenticator code
            // they don't have: they sign in with a recovery code, or an admin resets their two-factor. Failing closed
            // like that is safer than turning anyone's two-factor off.
            migrationBuilder.DropTable(
                name: "EmailCodes");

            migrationBuilder.DropColumn(
                name: "TwoFactorMethod",
                table: "AspNetUsers");
        }
    }
}
