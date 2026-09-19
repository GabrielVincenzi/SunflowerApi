using System.ComponentModel.DataAnnotations;

namespace SunflowerApi.Models
{
    public class DataRequest
    {
        // Clerk user id — injected server-side from HttpContext, never trusted from body
        public string? UserId { get; set; }

        [Required]
        [MinLength(10, ErrorMessage = "Message must be at least 10 characters.")]
        [MaxLength(2000, ErrorMessage = "Message cannot exceed 2000 characters.")]
        public string? Message { get; set; }
    }
}