using Microsoft.EntityFrameworkCore;
using SunflowerApi.Models;

namespace SunflowerApi.Data
{
    public class FilterDbContext : DbContext
    {
        public FilterDbContext(DbContextOptions<FilterDbContext> options) : base(options) { }

        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Source> Sources { get; set; } = null!; // <-- was missing

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("categories", "public");
                entity.Property(c => c.Name).HasColumnName("category");
                entity.Property(c => c.Description).HasColumnName("description");
                entity.Property(c => c.Lang).HasColumnName("lang");
            });

            modelBuilder.Entity<Source>(entity =>
            {
                entity.ToTable("sources", "public");
                entity.Property(s => s.Name).HasColumnName("source");
                entity.Property(s => s.Description).HasColumnName("description");
                entity.Property(s => s.Lang).HasColumnName("lang");
            });
        }
    }
}