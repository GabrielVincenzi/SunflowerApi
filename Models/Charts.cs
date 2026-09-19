using System.ComponentModel.DataAnnotations.Schema;

namespace SunflowerApi.Models
{
    [Table("charts", Schema = "public")]
    public class Chart
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("chart_id")]
        public Guid? ChartId { get; set; }

        public string? Category { get; set; }
        public string? Vars { get; set; }

        [Column("db_name")]
        public string? DbName { get; set; }

        [Column("chart_type")]
        public string? ChartType { get; set; }

        [Column("vector_dim")]
        public string? VectorDim { get; set; }
    }
}
