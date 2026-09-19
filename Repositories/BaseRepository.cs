using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

namespace SunflowerApi.Repositories
{
    public abstract class BaseRepository
    {
        protected readonly string ConnString;
        protected readonly ILogger Logger;
        protected static readonly char[] SplitComma = { ',' };

        protected BaseRepository(string connString, ILogger logger)
        {
            ConnString = connString;
            Logger = logger;
        }

        protected static double[]? ParseFromVectorString(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;

            s = s.Trim();

            // remove outer [ ] or ( )
            if (s.Length >= 2 && ((s[0] == '[' && s[^1] == ']') || (s[0] == '(' && s[^1] == ')')))
                s = s[1..^1];

            if (string.IsNullOrWhiteSpace(s))
                return null;

            var parts = s.Split(SplitComma, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<double>(parts.Length);

            foreach (var part in parts)
            {
                var span = part.AsSpan().Trim();
                if (double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    list.Add(v);
            }

            return list.Count == 0 ? null : list.ToArray();
        }

        protected async Task<HashSet<Guid>> LoadGuidSetAsync(
        NpgsqlConnection conn, string sql, string param, CancellationToken ct)
        {
            var set = new HashSet<Guid>();
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", param);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                if (!r.IsDBNull(0))
                    set.Add(r.GetGuid(0));
            return set;
        }

        protected async Task<(HashSet<Guid> allSeen, HashSet<Guid> recentSeen)> LoadSeenChartsAsync(
        NpgsqlConnection conn, string userId, int excludeDays, CancellationToken ct)
        {
            var allSeen = new HashSet<Guid>();
            var recent = new HashSet<Guid>();

            var sql = "SELECT object_id, event_time AT TIME ZONE 'UTC' FROM public.events WHERE user_id=@u AND action='seen'";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", userId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                if (r.IsDBNull(0)) continue;
                var id = r.GetGuid(0);
                allSeen.Add(id);
                if (!r.IsDBNull(1))
                {
                    var time = r.GetDateTime(1);
                    if (time >= DateTime.UtcNow.AddDays(-excludeDays))
                        recent.Add(id);
                }
            }

            return (allSeen, recent);
        }

        protected async Task<Dictionary<Guid, double[]>> LoadVectorsAsync(
        NpgsqlConnection conn, Guid[] ids, CancellationToken ct)
        {
            var vectors = new Dictionary<Guid, double[]>();
            if (ids.Length == 0) return vectors;

            var sql = "SELECT chart_id, vector_dim::text FROM public.charts WHERE chart_id = ANY(@ids)";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("ids", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Uuid, ids);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                if (!r.IsDBNull(1))
                    vectors[r.GetGuid(0)] = ParseFromVectorString(r.GetString(1))!;
            }

            return vectors;
        }


        protected async Task<List<string>> LoadRandomCategoriesAsync(
        NpgsqlConnection conn, string seed, int limit, string lang, CancellationToken ct)
        {
            var cats = new List<string>();
            var sql = @"
                SELECT text_input
                FROM (
                    SELECT DISTINCT ct.text_input, md5(concat(@seed, c.id::text)) AS sort_key
                    FROM public.charts_text ct
                    JOIN public.charts c ON ct.chart_id = c.chart_id
                    WHERE ct.text_category='category' AND ct.lang=@lang
                ) AS sub
                ORDER BY sort_key
                LIMIT @limit;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("seed", seed);
            cmd.Parameters.AddWithValue("lang", lang);
            cmd.Parameters.AddWithValue("limit", limit);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                cats.Add(r.GetString(0));

            return cats;
        }


        protected static double[] BuildAverageVector(
            Dictionary<Guid, double[]> vectors,
            HashSet<Guid> saved)
        {
            var dim = vectors.First().Value.Length;
            var avg = new double[dim];
            double total = 0;

            foreach (var (id, v) in vectors)
            {
                var w = saved.Contains(id) ? 2.0 : 1.0;
                for (int i = 0; i < dim; i++)
                    avg[i] += v[i] * w;
                total += w;
            }

            for (int i = 0; i < dim; i++)
                avg[i] /= total;

            return avg;
        }

        protected static string VectorToSql(double[] vec)
        {
            if (vec is null || vec.Length == 0)
                return "[]";

            return "[" + string.Join(",", vec.Select(d => d.ToString("G17", CultureInfo.InvariantCulture))) + "]";
        }

        protected static async Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(
            string sql,
            Action<NpgsqlCommand> bind,
            NpgsqlConnection conn,
            CancellationToken ct)
        {
            var rows = new List<Dictionary<string, object?>>();
            await using var cmd = new NpgsqlCommand(sql, conn);
            bind(cmd);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < r.FieldCount; i++)
                    row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                rows.Add(row);
            }
            return rows;
        }

        protected async Task<(List<Dictionary<string, object?>> Rows, long? Cursor, bool HasMore)>
            ExecuteSimpleCursorQueryAsync(
                string sql,
                Action<NpgsqlCommand> bind,
                CancellationToken ct)
        {
            var rows = new List<Dictionary<string, object?>>();
            long? lastId = null;

            await using var conn = new NpgsqlConnection(ConnString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            bind(cmd);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < r.FieldCount; i++)
                    row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                rows.Add(row);
            }

            if (rows.LastOrDefault()?.TryGetValue("id", out var v) == true)
                lastId = Convert.ToInt64(v);

            return (rows, lastId, rows.Count == cmd.Parameters["limit"].Value as int?);
        }
    }
}
