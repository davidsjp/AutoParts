using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoParts.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PrioritizeEngineDemoIgnitionCoil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Parts
                SET Description = 'Bobina de ignicao BMW N20 N26 F30 F32',
                    Category = 'Ignicao e motor',
                    Source = 'Referencia RealOEM - conferir VIN',
                    SuggestedValue = 289.90,
                    KeywordGroup = 'bobina ignicao bmw n20 n26 f30 f32 320i 328i 420i 428i',
                    Applications = '',
                    UpdatedAt = '2026-09-10 00:00:00+00:00'
                WHERE OemPartNumber = '12138616153';

                UPDATE PartCompatibilities
                SET Relevance = 0,
                    Status = 1,
                    Source = 'Catalogo amplo - revisar aplicacao',
                    UpdatedAt = '2026-09-10 00:00:00+00:00'
                WHERE PartId = (SELECT Id FROM Parts WHERE OemPartNumber = '12138616153')
                  AND VehicleId NOT IN (SELECT Id FROM Vehicles WHERE SerialNumber IN ('DEMO01', 'DEMO02', 'DEMO03', 'DEMO04'));

                UPDATE PartCompatibilities
                SET Relevance = CASE WHEN VehicleId IN (SELECT Id FROM Vehicles WHERE SerialNumber IN ('DEMO01', 'DEMO02')) THEN 10 ELSE 9 END,
                    Status = 1,
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
                SET Relevance = 5,
                    Status = 1,
                    Source = 'Existing catalog'
                WHERE PartId = (SELECT Id FROM Parts WHERE OemPartNumber = '12138616153');
                """);
        }
    }
}
