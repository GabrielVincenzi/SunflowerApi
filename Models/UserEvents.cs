using System.ComponentModel.DataAnnotations.Schema;

namespace SunflowerApi.Models
{
    [Table("events", Schema = "public")]
    public class UserEvent
    {
        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;
        public string Action { get; set; } = null!;
        [Column("object_id")]
        public Guid ObjectId { get; set; }
        [Column("event_time")]
        public DateTime Timestamp { get; set; }
    }
}