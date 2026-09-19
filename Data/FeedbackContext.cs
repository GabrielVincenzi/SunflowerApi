using Microsoft.EntityFrameworkCore;
using SunflowerApi.Models;

namespace SunflowerApi.Data
{
    public class FeedbackDbContext : DbContext
    {
        public FeedbackDbContext(DbContextOptions<FeedbackDbContext> options) : base(options) { }

        public DbSet<FeedbackItem> FeedbackItems { get; set; } = null!;
        public DbSet<FeedbackVote> FeedbackVotes { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FeedbackItem>(entity =>
            {
                entity.HasKey(f => f.Id);

                entity.HasMany(f => f.Votes)
                    .WithOne(v => v.FeedbackItem)
                    .HasForeignKey(v => v.FeedbackItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<FeedbackVote>(entity =>
            {
                // one vote per user per item
                entity.HasKey(v => new { v.FeedbackItemId, v.UserId });
                entity.Property(v => v.Direction).HasConversion<int>();
            });
        }
    }
}