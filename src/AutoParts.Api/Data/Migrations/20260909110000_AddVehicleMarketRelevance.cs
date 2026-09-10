using System;
using AutoParts.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoParts.Api.Data.Migrations;

[DbContext(typeof(AutoPartsDbContext))]
[Migration("20260909110000_AddVehicleMarketRelevance")]
public partial class AddVehicleMarketRelevance : Migration
{
    private const string Source = "BMW Group Brasil e Fenabrave, emplacamentos 2025";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "MarketRelevance", table: "Vehicles", type: "INTEGER", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<string>(name: "MarketRelevanceSource", table: "Vehicles", type: "TEXT", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "MarketRelevanceUpdatedAt", table: "Vehicles", type: "TEXT", nullable: true);

        migrationBuilder.Sql($"""
            UPDATE Vehicles
            SET MarketRelevance = CASE
                WHEN Manufacturer = 'BMW' AND Model LIKE 'X1 %' THEN 9
                WHEN Manufacturer = 'BMW' AND (Model = '335i' OR Model LIKE '335i %') THEN 8
                WHEN Manufacturer = 'BMW' AND (Model LIKE 'X3 %' OR Model LIKE 'M340i%') THEN 7
                WHEN Manufacturer = 'BMW' AND (Model IN ('116i', '118i', '120i', '125i') OR Model LIKE 'X4 %' OR Model LIKE 'X5 %') THEN 6
                WHEN Manufacturer = 'BMW' AND (Model LIKE '520%' OR Model LIKE '530%' OR Model LIKE '535%' OR Model LIKE '540%' OR Model LIKE '550%') THEN 5
                WHEN Manufacturer = 'BMW' AND (Model LIKE 'X2 %' OR Model LIKE 'X6 %') THEN 4
                WHEN Manufacturer = 'BMW' AND (Model LIKE '1%' OR Model LIKE '4%' OR Model LIKE 'Z4 %') THEN 3
                WHEN Manufacturer = 'BMW' AND Model LIKE 'M%' THEN 2
                WHEN Manufacturer = 'BMW' AND (Model LIKE '7%' OR Model LIKE 'X7 %') THEN 1
                ELSE 0
            END,
            MarketRelevanceSource = CASE WHEN Manufacturer = 'BMW' THEN '{Source}' ELSE NULL END,
            MarketRelevanceUpdatedAt = CASE WHEN Manufacturer = 'BMW' THEN '2026-09-09 00:00:00+00:00' ELSE NULL END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "MarketRelevance", table: "Vehicles");
        migrationBuilder.DropColumn(name: "MarketRelevanceSource", table: "Vehicles");
        migrationBuilder.DropColumn(name: "MarketRelevanceUpdatedAt", table: "Vehicles");
    }
}
