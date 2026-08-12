using AutoParts.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Data;

public sealed class AutoPartsDbContext(DbContextOptions<AutoPartsDbContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<PartCompatibility> PartCompatibilities => Set<PartCompatibility>();
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vehicle>().HasIndex(x => x.Vin).IsUnique();
        modelBuilder.Entity<Vehicle>().HasIndex(x => x.SerialNumber).IsUnique();
        modelBuilder.Entity<Part>().HasIndex(x => x.OemPartNumber).IsUnique();
        modelBuilder.Entity<PartCompatibility>()
            .HasIndex(x => new { x.PartId, x.VehicleId, x.ProductionStart }).IsUnique();
        modelBuilder.Entity<PartCompatibility>().HasOne(x => x.Part).WithMany(x => x.Compatibilities)
            .HasForeignKey(x => x.PartId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PartCompatibility>().HasOne(x => x.Vehicle).WithMany(x => x.Compatibilities)
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
    }
}
