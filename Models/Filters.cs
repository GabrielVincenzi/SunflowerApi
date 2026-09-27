using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SunflowerApi.Models
{
    public interface ILocalizedOption
    {
        string Name { get; }
        string? Description { get; }
        string Lang { get; }
    }

    [Table("categories", Schema = "public")]
    public class Category : ILocalizedOption
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        [Column("name")] public string Name { get; set; } = string.Empty;
        [Column("description")] public string? Description { get; set; }
        [Column("lang")] public string Lang { get; set; } = string.Empty;
    }

    [Table("sources", Schema = "public")]
    public class Source : ILocalizedOption
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        [Column("name")] public string Name { get; set; } = string.Empty;
        [Column("description")] public string? Description { get; set; }
        [Column("lang")] public string Lang { get; set; } = string.Empty;
    }

    public record FilterOptionDto(string Value, string Label);
}