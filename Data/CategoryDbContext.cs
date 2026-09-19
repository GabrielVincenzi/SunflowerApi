using Microsoft.EntityFrameworkCore;
using SunflowerApi.Models;

namespace SunflowerApi.Data
{
    public class CategoryDbContext : DbContext
    {
        public CategoryDbContext(DbContextOptions<CategoryDbContext> options) : base(options) { }

        public DbSet<Category> Categories { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("categories", "public");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Name).HasColumnName("category");
                entity.Property(c => c.Description).HasColumnName("description");
                entity.Property(c => c.Lang).HasColumnName("lang");
            });
        }
    }
}