using System.ComponentModel.DataAnnotations.Schema;

namespace SunflowerApi.Models
{
    [Table("dbs", Schema = "public")]
    public class DbMetadata
    {
        [Column("id")]
        public long Id { get; set; }
        [Column("db_name")]
        public string DbName { get; set; } = string.Empty;
        [Column("available_geos")]
        public string? AvailableGeos { get; set; }
        [Column("available_periods")]
        public string? AvailablePeriods { get; set; }
        [Column("db_source")]
        public string? DbSource { get; set; }
    }
}
