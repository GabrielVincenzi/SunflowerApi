using Microsoft.EntityFrameworkCore;
using SunflowerApi.Models;

namespace SunflowerApi.Data
{
    public class UserEventsDbContext : DbContext
    {
        public UserEventsDbContext(DbContextOptions<UserEventsDbContext> options) : base(options) { }
        public DbSet<UserEvent> UserEvents { get; set; } = null!;
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<UserEvent>().ToTable("events", "public");
            modelBuilder.Entity<UserEvent>().Property(c => c.UserId).HasColumnName("user_id");
            modelBuilder.Entity<UserEvent>().Property(c => c.ObjectId).HasColumnName("object_id");
            modelBuilder.Entity<UserEvent>().Property(c => c.Timestamp).HasColumnName("event_time");
        }
    }
}
