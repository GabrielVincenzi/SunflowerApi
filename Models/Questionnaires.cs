using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace SunflowerApi.Models
{
    [Table("questions", Schema = "public")]
    public class Question
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("object_id")]
        public Guid ObjectId { get; set; }

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("body")]
        public string Body { get; set; } = string.Empty;

        [Column("explanation")]
        public string? Explanation { get; set; }

        [Column("difficulty")]
        public short? Difficulty { get; set; }

        [Column("sponsor")]
        public string? Sponsor { get; set; }

        [Column("sponsor_body")]
        public string? SponsorBody { get; set; }

        [Column("sponsor_link")]
        public string? SponsorLink { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }

    [Table("choices", Schema = "public")]
    public class Choice
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("question_id")]
        public long QuestionId { get; set; }

        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Column("is_correct")]
        public bool IsCorrect { get; set; }
    }

    [Table("userquestionstates", Schema = "public")]
    public class UserQuestionState
    {
        [Column("user_id")]
        public string UserId { get; set; } = null!;

        [Column("question_id")]
        public long QuestionId { get; set; }

        [Column("next_due_at")]
        public DateTime? NextDueAt { get; set; }

        [Column("consecutive_correct")]
        public int ConsecutiveCorrect { get; set; } = 0;
    }

    public class QuestionDto
    {
        public long Id { get; set; }
        public Guid ObjectId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Explanation { get; set; }
        public short? Difficulty { get; set; }
        public string? Sponsor { get; set; }
        public string? SponsorBody { get; set; }
        public string? SponsorLink { get; set; }
        public List<ChoiceDto> Choices { get; set; } = new List<ChoiceDto>();
    }

    public class ChoiceDto
    {
        public long Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}
