using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoParts.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResetEngineDemoCompatibilitySelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE PartCompatibilities
                SET Relevance = 0,
                    Status = 3,
                    Source = 'Catalogo amplo - fora do exemplo N20/N26',
                    UpdatedAt = '2026-09-10 00:00:00+00:00'
                WHERE PartId = (SELECT Id FROM Parts WHERE OemPartNumber = '12138616153')
                  AND VehicleId NOT IN (SELECT Id FROM Vehicles WHERE SerialNumber IN ('DEMO01', 'DEMO02', 'DEMO03', 'DEMO04'));

                UPDATE PartCompatibilities
                SET Relevance = CASE WHEN VehicleId IN (SELECT Id FROM Vehicles WHERE SerialNumber IN ('DEMO01', 'DEMO02')) THEN 10 ELSE 9 END,
                    Status = 0,
                    Source = 'Referencia RealOEM - conferir VIN',
                    UpdatedAt = '2026-09-10 00:00:00+00:00'
                WHERE PartId = (SELECT Id FROM Parts WHERE OemPartNumber = '12138616153')
                  AND VehicleId IN (SELECT Id FROM Vehicles WHERE SerialNumber IN ('DEMO01', 'DEMO02', 'DEMO03', 'DEMO04'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE PartCompatibilities
                SET Status = 1,
                    Relevance = 5,
                    Source = 'Existing catalog'
                WHERE PartId = (SELECT Id FROM Parts WHERE OemPartNumber = '12138616153');
                """);
        }
    }
}
