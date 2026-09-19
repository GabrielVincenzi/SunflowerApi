using System.ComponentModel.DataAnnotations.Schema;

namespace SunflowerApi.Models
{
    [Table("translations", Schema = "public")]
    public class Translation
    {
        [Column("lang")]
        public string Lang { get; set; } = string.Empty;

        [Column("version")]
        public DateTime Version { get; set; }

        // JSONB is returned as raw JSON text
        [Column("payload")]
        public string Payload { get; set; } = string.Empty;
    }

    [Table("column_descriptions", Schema = "public")]
    public class ColumnLabels
    {
        [Column("table_name")]
        public string TableName { get; set; } = string.Empty;
        [Column("column_code")]
        public string ColumnCode { get; set; } = string.Empty;
        [Column("lang")]
        public string Lang { get; set; } = string.Empty;
        [Column("full_text")]
        public string Text { get; set; } = string.Empty;
    }
}