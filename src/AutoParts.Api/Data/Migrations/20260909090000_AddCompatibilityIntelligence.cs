using System;
using AutoParts.Api.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace AutoParts.Api.Data.Migrations;

[DbContext(typeof(AutoPartsDbContext))]
[Migration("20260909090000_AddCompatibilityIntelligence")]
public partial class AddCompatibilityIntelligence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "Confidence", table: "PartCompatibilities", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "ConfirmedAt", table: "PartCompatibilities", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ConfirmedByUserId", table: "PartCompatibilities", type: "TEXT", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "CreatedAt", table: "PartCompatibilities", type: "TEXT", nullable: false, defaultValue: new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero));
        migrationBuilder.AddColumn<string>(name: "EvidenceText", table: "PartCompatibilities", type: "TEXT", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<int>(name: "Relevance", table: "PartCompatibilities", type: "INTEGER", nullable: false, defaultValue: 5);
        migrationBuilder.AddColumn<string>(name: "Source", table: "PartCompatibilities", type: "TEXT", maxLength: 100, nullable: false, defaultValue: "Existing catalog");
        migrationBuilder.AddColumn<int>(name: "Status", table: "PartCompatibilities", type: "INTEGER", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "UpdatedAt", table: "PartCompatibilities", type: "TEXT", nullable: false, defaultValue: new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero));

        migrationBuilder.CreateTable(
            name: "CompatibilityReviewLogs",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                PartCompatibilityId = table.Column<int>(type: "INTEGER", nullable: false),
                Action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                PreviousStatus = table.Column<int>(type: "INTEGER", nullable: true),
                NewStatus = table.Column<int>(type: "INTEGER", nullable: false),
                PreviousRelevance = table.Column<int>(type: "INTEGER", nullable: true),
                NewRelevance = table.Column<int>(type: "INTEGER", nullable: false),
                PerformedByUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                PerformedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CompatibilityReviewLogs", x => x.Id);
                table.ForeignKey(
                    name: "FK_CompatibilityReviewLogs_PartCompatibilities_PartCompatibilityId",
                    column: x => x.PartCompatibilityId,
                    principalTable: "PartCompatibilities",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CompatibilityReviewLogs_PartCompatibilityId_PerformedAt",
            table: "CompatibilityReviewLogs",
            columns: new[] { "PartCompatibilityId", "PerformedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PartCompatibilities_PartId_Status_Relevance",
            table: "PartCompatibilities",
            columns: new[] { "PartId", "Status", "Relevance" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CompatibilityReviewLogs");
        migrationBuilder.DropIndex(name: "IX_PartCompatibilities_PartId_Status_Relevance", table: "PartCompatibilities");
        migrationBuilder.DropColumn(name: "Confidence", table: "PartCompatibilities");
        migrationBuilder.DropColumn(name: "ConfirmedAt", table: "PartCompatibilities");
        migrationBuilder.DropColumn(name: "ConfirmedByUserId", table: "PartCompatibilities");
        migrationBuilder.DropColumn(name: "CreatedAt", table: "PartCompatibilities");
        migrationBuilder.DropColumn(name: "EvidenceText", table: "PartCompatibilities");
        migrationBuilder.DropColumn(name: "Relevance", table: "PartCompatibilities");
        migrationBuilder.DropColumn(name: "Source", table: "PartCompatibilities");
        migrationBuilder.DropColumn(name: "Status", table: "PartCompatibilities");
        migrationBuilder.DropColumn(name: "UpdatedAt", table: "PartCompatibilities");
    }
}
