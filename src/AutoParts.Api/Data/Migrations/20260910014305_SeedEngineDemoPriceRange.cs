using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoParts.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedEngineDemoPriceRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Parts
                SET SuggestedValue = 289.90
                WHERE OemPartNumber = '12138616153';

                INSERT INTO PartPriceObservations (PartId, Amount, Source, ObservedAt, IsSample)
                SELECT Id, 219.90, 'Exemplo ficticio', '2026-09-10 00:00:00+00:00', 1
                FROM Parts
                WHERE OemPartNumber = '12138616153';

                INSERT INTO PartPriceObservations (PartId, Amount, Source, ObservedAt, IsSample)
                SELECT Id, 359.90, 'Exemplo ficticio', '2026-09-10 00:00:00+00:00', 1
                FROM Parts
                WHERE OemPartNumber = '12138616153';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM PartPriceObservations
                WHERE PartId = (SELECT Id FROM Parts WHERE OemPartNumber = '12138616153')
                  AND Source = 'Exemplo ficticio';
                """);
        }
    }
}
