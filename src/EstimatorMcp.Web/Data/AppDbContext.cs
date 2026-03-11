using Microsoft.EntityFrameworkCore;

namespace EstimatorMcp.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TechStackEntity> TechStacks => Set<TechStackEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<CatalogEntryEntity> CatalogEntries => Set<CatalogEntryEntity>();
    public DbSet<EntryEstimateEntity> EntryEstimates => Set<EntryEstimateEntity>();
    public DbSet<CatalogVersionEntity> CatalogVersions => Set<CatalogVersionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EntryEstimateEntity>()
            .HasKey(e => new { e.EntryId, e.RoleId });

        modelBuilder.Entity<RoleEntity>()
            .HasOne(r => r.TechStack)
            .WithMany(ts => ts.Roles)
            .HasForeignKey(r => r.TechStackId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CatalogEntryEntity>()
            .HasOne(e => e.TechStack)
            .WithMany(ts => ts.Entries)
            .HasForeignKey(e => e.TechStackId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<EntryEstimateEntity>()
            .HasOne(e => e.Entry)
            .WithMany(ce => ce.Estimates)
            .HasForeignKey(e => e.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EntryEstimateEntity>()
            .HasOne(e => e.Role)
            .WithMany(r => r.Estimates)
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
