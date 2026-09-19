using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SunflowerApi.Models
{
    public enum VoteDirection
    {
        Up = 1,
        Down = -1
    }

    [Table("feedback_items", Schema = "public")]
    public class FeedbackItem
    {
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("message")]
        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        // Clerk user id of whoever submitted this — not exposed to clients directly.
        [Column("author_id")]
        public string AuthorId { get; set; } = string.Empty;

        // Denormalized display name captured at submission time, so we don't
        // need a round trip to Clerk just to render the list.
        [Column("author_name")]
        public string? AuthorName { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<FeedbackVote> Votes { get; set; } = new List<FeedbackVote>();
    }

    [Table("feedback_votes", Schema = "public")]
    public class FeedbackVote
    {
        [Column("feedback_item_id")]
        public Guid FeedbackItemId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("direction")]
        public VoteDirection Direction { get; set; }

        public FeedbackItem FeedbackItem { get; set; } = null!;
    }

    // Shape returned to the client — matches FeedbackItem in the app's feedback.ts exactly.
    public record FeedbackItemDto(
        Guid Id,
        string Message,
        string? AuthorName,
        int Upvotes,
        int Downvotes,
        int Score,
        string? MyVote,
        DateTime CreatedAt
    );
}