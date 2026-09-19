using Microsoft.EntityFrameworkCore;
using SunflowerApi.Models;

namespace SunflowerApi.Data
{
    public class DbMetadataDbContext : DbContext
    {
        public DbMetadataDbContext(DbContextOptions<DbMetadataDbContext> options) : base(options) { }
        public DbSet<DbMetadata> DbsMetadata { get; set; } = null!;
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbMetadata>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasColumnName("id");
            entity.Property(c => c.DbName).HasColumnName("db_name");
            entity.Property(c => c.AvailableGeos).HasColumnName("available_geos");
            entity.Property(c => c.AvailablePeriods).HasColumnName("available_periods");
            entity.Property(c => c.DbSource).HasColumnName("db_source");
        });
        }
    }
}
