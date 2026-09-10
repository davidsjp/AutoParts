using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoParts.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedEngineDemoIgnitionCoil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO Parts (OemPartNumber, Description, Category, Source, CatalogDate, SuggestedValue, KeywordGroup, Applications, CreatedAt, UpdatedAt)
                SELECT '12138616153', 'Bobina de ignicao BMW N20 N26', 'Ignicao e motor', 'Referencia RealOEM - conferir VIN', '2026-09-10', 289.90,
                       'bobina ignicao bmw n20 n26 f30 f32 320i 328i 420i 428i', '', '2026-09-10 00:00:00+00:00', '2026-09-10 00:00:00+00:00'
                WHERE NOT EXISTS (SELECT 1 FROM Parts WHERE OemPartNumber = '12138616153');

                INSERT INTO Vehicles (Manufacturer, Model, Chassis, Engine, ModelYear, ProductionDate, SerialNumber, Market, TypeCode, MarketRelevance, MarketRelevanceSource, MarketRelevanceUpdatedAt)
                SELECT 'BMW', '320i', 'F30', 'N20', 2012, '2012-01-01', 'DEMO01', 'BR', NULL, 8, 'Exemplo de catalogo de motor', '2026-09-10 00:00:00+00:00' WHERE NOT EXISTS (SELECT 1 FROM Vehicles WHERE SerialNumber = 'DEMO01');
                INSERT INTO Vehicles (Manufacturer, Model, Chassis, Engine, ModelYear, ProductionDate, SerialNumber, Market, TypeCode, MarketRelevance, MarketRelevanceSource, MarketRelevanceUpdatedAt)
                SELECT 'BMW', '328i', 'F30', 'N26', 2012, '2012-01-01', 'DEMO02', 'BR', NULL, 8, 'Exemplo de catalogo de motor', '2026-09-10 00:00:00+00:00' WHERE NOT EXISTS (SELECT 1 FROM Vehicles WHERE SerialNumber = 'DEMO02');
                INSERT INTO Vehicles (Manufacturer, Model, Chassis, Engine, ModelYear, ProductionDate, SerialNumber, Market, TypeCode, MarketRelevance, MarketRelevanceSource, MarketRelevanceUpdatedAt)
                SELECT 'BMW', '420i', 'F32', 'N20', 2013, '2013-01-01', 'DEMO03', 'BR', NULL, 7, 'Exemplo de catalogo de motor', '2026-09-10 00:00:00+00:00' WHERE NOT EXISTS (SELECT 1 FROM Vehicles WHERE SerialNumber = 'DEMO03');
                INSERT INTO Vehicles (Manufacturer, Model, Chassis, Engine, ModelYear, ProductionDate, SerialNumber, Market, TypeCode, MarketRelevance, MarketRelevanceSource, MarketRelevanceUpdatedAt)
                SELECT 'BMW', '428i', 'F32', 'N26', 2013, '2013-01-01', 'DEMO04', 'BR', NULL, 7, 'Exemplo de catalogo de motor', '2026-09-10 00:00:00+00:00' WHERE NOT EXISTS (SELECT 1 FROM Vehicles WHERE SerialNumber = 'DEMO04');

                INSERT INTO PartCompatibilities (PartId, VehicleId, ProductionStart, ProductionEnd, Notes, Relevance, Status, Confidence, Source, EvidenceText, ConfirmedByUserId, ConfirmedAt, CreatedAt, UpdatedAt)
                SELECT p.Id, v.Id, '2012-01-01', '2016-12-31', 'Aplicacao de demonstracao: conferir por VIN antes de publicar.', 10, 1, 85, 'Referencia RealOEM', 'Bobina de ignicao OEM 12138616153, aplicacao N20/N26.', NULL, NULL, '2026-09-10 00:00:00+00:00', '2026-09-10 00:00:00+00:00' FROM Parts p JOIN Vehicles v ON v.SerialNumber = 'DEMO01' WHERE p.OemPartNumber = '12138616153' AND NOT EXISTS (SELECT 1 FROM PartCompatibilities pc WHERE pc.PartId = p.Id AND pc.VehicleId = v.Id AND pc.ProductionStart = '2012-01-01');
                INSERT INTO PartCompatibilities (PartId, VehicleId, ProductionStart, ProductionEnd, Notes, Relevance, Status, Confidence, Source, EvidenceText, ConfirmedByUserId, ConfirmedAt, CreatedAt, UpdatedAt)
                SELECT p.Id, v.Id, '2012-01-01', '2016-12-31', 'Aplicacao de demonstracao: conferir por VIN antes de publicar.', 10, 1, 85, 'Referencia RealOEM', 'Bobina de ignicao OEM 12138616153, aplicacao N20/N26.', NULL, NULL, '2026-09-10 00:00:00+00:00', '2026-09-10 00:00:00+00:00' FROM Parts p JOIN Vehicles v ON v.SerialNumber = 'DEMO02' WHERE p.OemPartNumber = '12138616153' AND NOT EXISTS (SELECT 1 FROM PartCompatibilities pc WHERE pc.PartId = p.Id AND pc.VehicleId = v.Id AND pc.ProductionStart = '2012-01-01');
                INSERT INTO PartCompatibilities (PartId, VehicleId, ProductionStart, ProductionEnd, Notes, Relevance, Status, Confidence, Source, EvidenceText, ConfirmedByUserId, ConfirmedAt, CreatedAt, UpdatedAt)
                SELECT p.Id, v.Id, '2013-01-01', '2016-12-31', 'Aplicacao de demonstracao: conferir por VIN antes de publicar.', 9, 1, 85, 'Referencia RealOEM', 'Bobina de ignicao OEM 12138616153, aplicacao N20/N26.', NULL, NULL, '2026-09-10 00:00:00+00:00', '2026-09-10 00:00:00+00:00' FROM Parts p JOIN Vehicles v ON v.SerialNumber = 'DEMO03' WHERE p.OemPartNumber = '12138616153' AND NOT EXISTS (SELECT 1 FROM PartCompatibilities pc WHERE pc.PartId = p.Id AND pc.VehicleId = v.Id AND pc.ProductionStart = '2013-01-01');
                INSERT INTO PartCompatibilities (PartId, VehicleId, ProductionStart, ProductionEnd, Notes, Relevance, Status, Confidence, Source, EvidenceText, ConfirmedByUserId, ConfirmedAt, CreatedAt, UpdatedAt)
                SELECT p.Id, v.Id, '2013-01-01', '2016-12-31', 'Aplicacao de demonstracao: conferir por VIN antes de publicar.', 9, 1, 85, 'Referencia RealOEM', 'Bobina de ignicao OEM 12138616153, aplicacao N20/N26.', NULL, NULL, '2026-09-10 00:00:00+00:00', '2026-09-10 00:00:00+00:00' FROM Parts p JOIN Vehicles v ON v.SerialNumber = 'DEMO04' WHERE p.OemPartNumber = '12138616153' AND NOT EXISTS (SELECT 1 FROM PartCompatibilities pc WHERE pc.PartId = p.Id AND pc.VehicleId = v.Id AND pc.ProductionStart = '2013-01-01');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM Parts WHERE OemPartNumber = '12138616153';
                DELETE FROM Vehicles WHERE SerialNumber IN ('DEMO01', 'DEMO02', 'DEMO03', 'DEMO04');
                """);
        }
    }
}
