using AutoParts.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Data;

/// <summary>
/// EF Core boundary for the local catalog. Controllers and services should use
/// this context for persisted vehicles, parts, compatibility rows and import logs.
/// </summary>
public sealed class AutoPartsDbContext(DbContextOptions<AutoPartsDbContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<PartCompatibility> PartCompatibilities => Set<PartCompatibility>();
    public DbSet<CompatibilityReviewLog> CompatibilityReviewLogs => Set<CompatibilityReviewLog>();
    public DbSet<PartPriceObservation> PartPriceObservations => Set<PartPriceObservation>();
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // These uniqueness rules protect the commercial catalog from duplicate
        // VINs, external vehicle identities, OEM numbers and compatibility rows.
        modelBuilder.Entity<Vehicle>().HasIndex(x => x.Vin).IsUnique();
        modelBuilder.Entity<Vehicle>().HasIndex(x => x.SerialNumber).IsUnique();
        modelBuilder.Entity<Vehicle>().HasIndex(x => new { x.Manufacturer, x.Model, x.Chassis, x.Engine, x.TypeCode });
        modelBuilder.Entity<Part>().HasIndex(x => x.OemPartNumber).IsUnique();
        modelBuilder.Entity<Part>().HasIndex(x => x.Side);
        modelBuilder.Entity<Part>().HasIndex(x => x.Position);
        modelBuilder.Entity<PartCompatibility>()
            .HasIndex(x => new { x.PartId, x.VehicleId, x.ProductionStart }).IsUnique();
        modelBuilder.Entity<PartCompatibility>()
            .HasIndex(x => new { x.PartId, x.Status, x.Relevance });
        modelBuilder.Entity<PartCompatibility>().HasOne(x => x.Part).WithMany(x => x.Compatibilities)
            .HasForeignKey(x => x.PartId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PartCompatibility>().HasOne(x => x.Vehicle).WithMany(x => x.Compatibilities)
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CompatibilityReviewLog>().HasIndex(x => new { x.PartCompatibilityId, x.PerformedAt });
        modelBuilder.Entity<CompatibilityReviewLog>().HasOne(x => x.PartCompatibility).WithMany(x => x.ReviewLogs)
            .HasForeignKey(x => x.PartCompatibilityId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PartPriceObservation>().HasIndex(x => new { x.PartId, x.ObservedAt });
        modelBuilder.Entity<PartPriceObservation>().HasOne(x => x.Part).WithMany(x => x.PriceObservations)
            .HasForeignKey(x => x.PartId).OnDelete(DeleteBehavior.Cascade);
    }
}
