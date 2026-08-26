using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoParts.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StructuredCompatibilityMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Position",
                table: "Parts",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Side",
                table: "Parts",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Manufacturer_Model_Chassis_Engine_TypeCode",
                table: "Vehicles",
                columns: new[] { "Manufacturer", "Model", "Chassis", "Engine", "TypeCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Parts_Position",
                table: "Parts",
                column: "Position");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_Side",
                table: "Parts",
                column: "Side");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_Manufacturer_Model_Chassis_Engine_TypeCode",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Parts_Position",
                table: "Parts");

            migrationBuilder.DropIndex(
                name: "IX_Parts_Side",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "Side",
                table: "Parts");
        }
    }
}
