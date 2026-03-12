using Microsoft.EntityFrameworkCore;

namespace CatalogEditor.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PendingVerificationEntity> PendingVerifications => Set<PendingVerificationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PendingVerificationEntity>(e =>
        {
            e.HasKey(v => v.Id);
            e.HasIndex(v => v.Email);
        });
    }
}
