using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoParts.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Applications",
                table: "Parts",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "CatalogDate",
                table: "Parts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeywordGroup",
                table: "Parts",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SuggestedValue",
                table: "Parts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Applications",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "CatalogDate",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "KeywordGroup",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "SuggestedValue",
                table: "Parts");
        }
    }
}
