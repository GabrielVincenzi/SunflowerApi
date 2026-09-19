using Microsoft.EntityFrameworkCore;
using SunflowerApi.Models;

namespace SunflowerApi.Data
{
    public class QuestionnaireDbContext : DbContext
    {
        public QuestionnaireDbContext(DbContextOptions<QuestionnaireDbContext> opts) : base(opts) { }

        public DbSet<Question> Questions { get; set; } = null!;
        public DbSet<Choice> Choices { get; set; } = null!;
        public DbSet<UserQuestionState> UserQuestionStates { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Question>().HasKey(q => q.Id);
            modelBuilder.Entity<Choice>().HasKey(c => c.Id);
            modelBuilder.Entity<UserQuestionState>().HasKey(u => new { u.UserId, u.QuestionId });

            base.OnModelCreating(modelBuilder);
        }
    }
}
