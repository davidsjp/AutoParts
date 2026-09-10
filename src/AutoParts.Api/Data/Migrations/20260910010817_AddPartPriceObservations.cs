using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoParts.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartPriceObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartPriceObservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsSample = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartPriceObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartPriceObservations_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartPriceObservations_PartId_ObservedAt",
                table: "PartPriceObservations",
                columns: new[] { "PartId", "ObservedAt" });

            migrationBuilder.Sql("""
                UPDATE Parts
                SET SuggestedValue = 459.90
                WHERE OemPartNumber = '51217202146' AND SuggestedValue IS NULL;

                INSERT INTO PartPriceObservations (PartId, Amount, Source, ObservedAt, IsSample)
                SELECT Id, 389.90, 'Exemplo ficticio', '2026-09-10 00:00:00+00:00', 1
                FROM Parts
                WHERE OemPartNumber = '51217202146';

                INSERT INTO PartPriceObservations (PartId, Amount, Source, ObservedAt, IsSample)
                SELECT Id, 529.90, 'Exemplo ficticio', '2026-09-10 00:00:00+00:00', 1
                FROM Parts
                WHERE OemPartNumber = '51217202146';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartPriceObservations");
        }
    }
}
