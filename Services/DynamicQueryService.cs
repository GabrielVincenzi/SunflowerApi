using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using System.Text.RegularExpressions;

namespace SunflowerApi.Services
{
    public class DynamicQueryService : IDynamicQueryService
    {
        private readonly string _connectionString;
        private readonly ILogger<DynamicQueryService> _logger;

        // Validate table names: allow only letters, numbers, underscore
        private static readonly Regex TableNameRegex = new(@"^[A-Za-z0-9_]+$");

        public DynamicQueryService(IConfiguration config, ILogger<DynamicQueryService> logger)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection is required");
            _logger = logger;
        }

        public async Task<List<Dictionary<string, object?>>> QueryTableAsync(
            string tableName,
            string[]? geos = null,
            DateTime? startPeriod = null,
            DateTime? endPeriod = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("tableName is required.", nameof(tableName));

            // Basic validation: avoid quotes/../injection - only allow simple names.
            if (!TableNameRegex.IsMatch(tableName))
                throw new ArgumentException("Invalid table name.", nameof(tableName));

            if (geos?.Any(g => string.IsNullOrWhiteSpace(g)) == true)
                throw new ArgumentException("Geographic filters cannot be empty");

            if (startPeriod.HasValue && endPeriod.HasValue && startPeriod > endPeriod)
                throw new ArgumentException("Start period cannot be after end period");

            var rows = new List<Dictionary<string, object?>>();

            // Use Npgsql for safe array parameters and robust typing
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            // build SQL
            var sql = $"SELECT * FROM public.\"{tableName}\" WHERE 1=1";
            var cmd = conn.CreateCommand();

            if (geos?.Length > 0)
            {
                sql += " AND geo = ANY(@geos)";
                var p = new NpgsqlParameter("geos", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text)
                {
                    Value = geos
                };
                cmd.Parameters.Add(p);
            }

            if (startPeriod.HasValue)
            {
                sql += " AND time_period >= @start";
                cmd.Parameters.Add(new NpgsqlParameter("start", NpgsqlTypes.NpgsqlDbType.Timestamp) { Value = startPeriod.Value });
            }

            if (endPeriod.HasValue)
            {
                sql += " AND time_period <= @end";
                cmd.Parameters.Add(new NpgsqlParameter("end", NpgsqlTypes.NpgsqlDbType.Timestamp) { Value = endPeriod.Value });
            }

            cmd.CommandText = sql;
            cmd.CommandTimeout = 30;

            try
            {
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var val = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
                        row[reader.GetName(i)] = val;
                    }
                    rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QueryTableAsync failed for table {Table}", tableName);
                throw;
            }

            return rows;
        }
    }
}
