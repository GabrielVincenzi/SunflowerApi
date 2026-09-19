using Microsoft.EntityFrameworkCore;
using SunflowerApi.Models;

namespace SunflowerApi.Data
{
    public class TranslationDbContext : DbContext
    {
        public TranslationDbContext(DbContextOptions<TranslationDbContext> options)
            : base(options) { }

        public DbSet<Translation> Translations { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Translation>(entity =>
            {
                entity.ToTable("translations", "public");
                entity.HasKey(e => e.Lang);
                entity.Property(e => e.Lang).HasColumnName("lang");
                entity.Property(e => e.Version).HasColumnName("version");
                entity.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb");
            });
        }
    }
}
