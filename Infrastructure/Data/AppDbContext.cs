using FFAggerator.Domain;
using Microsoft.EntityFrameworkCore;

namespace FFAggerator.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<WeeklyProjection> Projections => Set<WeeklyProjection>();
    public DbSet<PlayerNewsItem> News => Set<PlayerNewsItem>();
    public DbSet<CacheMeta> Meta => Set<CacheMeta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.SearchName);
            e.HasIndex(p => p.SearchRank);
            e.Property(p => p.Id).HasMaxLength(32);
            e.Property(p => p.FullName).HasMaxLength(120);
            e.Property(p => p.Team).HasMaxLength(8);
            e.Property(p => p.Position).HasMaxLength(8);
        });

        modelBuilder.Entity<WeeklyProjection>(e =>
        {
            e.HasIndex(p => new { p.PlayerId, p.Season, p.Week, p.SeasonType }).IsUnique();
            e.HasOne(p => p.Player).WithMany().HasForeignKey(p => p.PlayerId);
        });

        modelBuilder.Entity<PlayerNewsItem>(e =>
        {
            e.HasIndex(n => n.Url);
            e.HasIndex(n => new { n.PlayerId, n.Url }).IsUnique();
            e.HasOne(n => n.Player).WithMany().HasForeignKey(n => n.PlayerId);
            e.Property(n => n.Url).HasMaxLength(2000);
        });

        modelBuilder.Entity<CacheMeta>(e =>
        {
            e.HasKey(m => m.Key);
        });
    }
}
