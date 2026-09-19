using Microsoft.EntityFrameworkCore;
using SunflowerApi.Models;

namespace SunflowerApi.Data
{
    public class DictionaryDbContext : DbContext
    {
        public DictionaryDbContext(DbContextOptions<DictionaryDbContext> options) : base(options) { }

        public DbSet<ColumnLabels> ColumnLabels { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ColumnLabels>(entity =>
            {
                entity.HasKey(c => new { c.TableName, c.ColumnCode, c.Lang });

                entity.Property(c => c.TableName).HasColumnName("table_name");
                entity.Property(c => c.ColumnCode).HasColumnName("column_code");
                entity.Property(c => c.Lang).HasColumnName("lang");
                entity.Property(c => c.Text).HasColumnName("full_text");
            });
        }
    }
}