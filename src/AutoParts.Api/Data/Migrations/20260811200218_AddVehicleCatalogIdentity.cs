using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoParts.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleCatalogIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Market",
                table: "Vehicles",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "Vehicles",
                type: "TEXT",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TypeCode",
                table: "Vehicles",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_SerialNumber",
                table: "Vehicles",
                column: "SerialNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_SerialNumber",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Market",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "TypeCode",
                table: "Vehicles");
        }
    }
}
