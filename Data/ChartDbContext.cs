using Microsoft.EntityFrameworkCore;
using SunflowerApi.Models;

namespace SunflowerApi.Data
{
    public class ChartDbContext : DbContext
    {
        public ChartDbContext(DbContextOptions<ChartDbContext> options) : base(options) { }

        public DbSet<Chart> Charts { get; set; } = null!;
        public DbSet<SearchableAsset> SearchableAssets => Set<SearchableAsset>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Chart>().ToTable("charts", "public");
            modelBuilder.Entity<Chart>().HasKey(c => c.ChartId);
            modelBuilder.Entity<Chart>().Property(c => c.ChartId).HasColumnName("chart_id");
            modelBuilder.Entity<Chart>().Property(c => c.ChartType).HasColumnName("chart_type");
            modelBuilder.Entity<Chart>().Property(c => c.DbName).HasColumnName("db_name");
            modelBuilder.Entity<Chart>().Property(c => c.VectorDim).HasColumnName("vector_dim");

            modelBuilder.Entity<SearchableAsset>(entity =>
        {
            entity.ToTable("searchable_assets");

            entity.Property(e => e.Embedding)
                  .HasColumnType("vector(384)");

            // IVFFlat index: splits the vector space into 'lists' Voronoi cells.
            // With <100k rows use lists=100. With >1M rows use lists=1000.
            // The index makes approximate nearest-neighbor search very fast (~5-10ms).
            entity.HasIndex(e => e.Embedding)
                  .HasMethod("ivfflat")
                  .HasOperators("vector_cosine_ops")
                  .HasStorageParameter("lists", 100);
        });
        }
    }
}
