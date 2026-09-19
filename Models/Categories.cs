using System.ComponentModel.DataAnnotations.Schema;

namespace SunflowerApi.Models
{
    [Table("categories", Schema = "public")]
    public class Category
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("category")]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }
        [Column("lang")]
        public string Lang { get; set; } = string.Empty;
    }

    public record CategoryDto(string Category, string Name);
}