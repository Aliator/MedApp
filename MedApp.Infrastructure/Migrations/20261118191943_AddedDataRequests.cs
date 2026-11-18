using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedDataRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedByPatient = table.Column<bool>(type: "bit", nullable: false),
                    RequesterName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequesterRelationship = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IdentityCheckedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IdentityCheckedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdentityCheckedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IdentityCheckMethod = table.Column<int>(type: "int", nullable: true),
                    IdentityCheckDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ExtensionMonths = table.Column<int>(type: "int", nullable: false),
                    ExtensionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RefusalReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SentOn = table.Column<DateOnly>(type: "date", nullable: true),
                    SendMethod = table.Column<int>(type: "int", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DueSoonNotifiedFor = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataRequests_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DataRequestWithheldItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataRequestWithheldItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataRequestWithheldItems_DataRequests_DataRequestId",
                        column: x => x.DataRequestId,
                        principalTable: "DataRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataRequests_PatientId",
                table: "DataRequests",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_DataRequests_Status_DueOn",
                table: "DataRequests",
                columns: new[] { "Status", "DueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_DataRequestWithheldItems_DataRequestId_Kind_ItemId",
                table: "DataRequestWithheldItems",
                columns: new[] { "DataRequestId", "Kind", "ItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataRequestWithheldItems");

            migrationBuilder.DropTable(
                name: "DataRequests");
        }
    }
}
