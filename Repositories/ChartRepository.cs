using Npgsql;
using NpgsqlTypes;
using System.Data;

namespace SunflowerApi.Repositories
{
    public class ChartRepository : BaseRepository, IChartRepository
    {
        private readonly IDictionaryRepository _dictionaryRepository;
        public ChartRepository(
            IConfiguration cfg,
            ILogger<ChartRepository> logger,
            IDictionaryRepository dictionaryRepository)
            : base(
                cfg.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("DefaultConnection is required"),
                logger)
        {
            _dictionaryRepository = dictionaryRepository;
        }

        // ------------------------------------------------------------------
        // Selected charts
        // ------------------------------------------------------------------
        public async Task<(List<Dictionary<string, object?>> Rows, long? NextCursor, bool HasMore)>
            GetSelectedChartsAsync(
                string? category,
                string? search,
                int limit = 5,
                long? afterId = null,
                string lang = "en",
                CancellationToken ct = default)
        {
            limit = Math.Clamp(limit, 1, 100);

            var sql = @"
                WITH s AS (
                    SELECT
                        c.id, c.chart_id, c.chart_type, c.vars, c.db_name,
                        ct.title, ct.description, ct.category
                    FROM public.charts c
                    LEFT JOIN public.charts_text ct ON ct.chart_id = c.chart_id AND ct.lang = @lang
                )
                SELECT * FROM s
                WHERE 1=1";

            if (afterId.HasValue) sql += " AND id > @afterId";
            if (!string.IsNullOrWhiteSpace(category)) sql += " AND category ILIKE @category";
            if (!string.IsNullOrWhiteSpace(search)) sql += " AND (title ILIKE @search OR description ILIKE @search)";
            sql += " ORDER BY id ASC LIMIT @limit;";

            var (rows, nextCursor, hasMore) = await ExecuteSimpleCursorQueryAsync(sql, cmd =>
            {
                cmd.Parameters.AddWithValue("lang", lang);
                cmd.Parameters.AddWithValue("limit", limit);
                if (afterId.HasValue) cmd.Parameters.AddWithValue("afterId", afterId.Value);
                if (!string.IsNullOrWhiteSpace(category)) cmd.Parameters.AddWithValue("category", $"%{category}%");
                if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("search", $"%{search}%");
            }, ct);

            return (rows, nextCursor, hasMore);
        }

        // ------------------------------------------------------------------
        // Recommended charts (vector similarity)
        // ------------------------------------------------------------------
        public async Task<(List<Dictionary<string, object?>> Rows, object? NextCursor, bool HasMore)>
        GetRecommendedChartsAsync(
            string userId,
            int limit = 5,
            int excludeSeenDays = 2,
            string lang = "en",
            double? lastSimilarity = null,
            long? afterId = null,
            CancellationToken ct = default)
        {
            limit = Math.Clamp(limit, 1, 100);

            await using var conn = new NpgsqlConnection(ConnString);
            await conn.OpenAsync(ct);

            try
            {
                var saved = await LoadGuidSetAsync(conn,
                    "SELECT object_id FROM public.saved WHERE user_id=@u",
                    userId, ct);

                var (seen, seenRecent) = await LoadSeenChartsAsync(conn, userId, excludeSeenDays, ct);

                var profileIds = saved.Union(seen).ToArray();
                if (profileIds.Length == 0)
                    return (new(), null, false);

                var vectors = await LoadVectorsAsync(conn, profileIds, ct);
                if (vectors.Count == 0)
                    return (new(), null, false);

                var avgVector = BuildAverageVector(vectors, saved);

                var sql = @"
                    WITH s AS (
                        SELECT
                            c.id, c.chart_id, c.chart_type, c.vars, c.db_name,
                            ct.title, ct.description, ct.category,
                            1 - (c.vector_dim <=> @q::vector) AS sim
                        FROM public.charts c
                        LEFT JOIN public.charts_text ct
                            ON ct.chart_id=c.chart_id AND ct.lang=@lang
                        WHERE NOT (c.chart_id = ANY(@exclude))
                    )
                    SELECT * FROM s
                    WHERE 1=1";

                if (lastSimilarity.HasValue && afterId.HasValue)
                    sql += " AND (sim < @ls OR (sim = @ls AND id > @lid))";

                sql += " ORDER BY sim DESC, id ASC LIMIT @limit;";

                var rows = await ExecuteReaderAsync(sql, cmd =>
                {
                    cmd.Parameters.AddWithValue("q", VectorToSql(avgVector));
                    cmd.Parameters.AddWithValue("lang", lang);
                    cmd.Parameters.AddWithValue("exclude", NpgsqlDbType.Array | NpgsqlDbType.Uuid, saved.Union(seenRecent).ToArray());
                    cmd.Parameters.AddWithValue("limit", limit);

                    if (lastSimilarity.HasValue && afterId.HasValue)
                    {
                        cmd.Parameters.AddWithValue("ls", lastSimilarity.Value);
                        cmd.Parameters.AddWithValue("lid", afterId.Value);
                    }
                }, conn, ct);

                var last = rows.LastOrDefault();
                return (
                    rows,
                    last == null ? null : new
                    {
                        lastSimilarity = Convert.ToDouble(last["sim"]),
                        lastId = Convert.ToInt64(last["id"])
                    },
                    rows.Count == limit
                );
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GetRecommendedChartsAsync failed");
                throw;
            }
        }

        // ------------------------------------------------------------------
        // Random charts (deterministic)
        // ------------------------------------------------------------------
        public async Task<(List<Dictionary<string, object?>> Rows, object? NextCursor, bool HasMore)>
        GetRandomChartsAsync(
            int limit = 5,
            int categories = 1,
            string lang = "en",
            string? seed = null,
            string? lastSortKey = null,
            long? afterId = null,
            CancellationToken ct = default)
        {
            limit = Math.Clamp(limit, 1, 100);
            categories = Math.Max(1, categories);
            seed ??= Guid.NewGuid().ToString("N");

            await using var conn = new NpgsqlConnection(ConnString);
            await conn.OpenAsync(ct);

            // -----------------------
            // Pick deterministic categories
            // -----------------------
            var cats = new List<string>();
            var sqlCats = @"
                    SELECT category
                    FROM (
                        SELECT DISTINCT ct.category, md5(concat(@seed, c.id::text)) AS sort_key
                        FROM public.charts_text ct
                        JOIN public.charts c ON ct.chart_id = c.chart_id
                        WHERE ct.lang = @lang
                    ) AS sub
                    ORDER BY sort_key
                    LIMIT @n;";

            await using (var cmd = new NpgsqlCommand(sqlCats, conn))
            {
                cmd.Parameters.AddWithValue("seed", seed);
                cmd.Parameters.AddWithValue("lang", lang);
                cmd.Parameters.AddWithValue("n", categories);

                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                    cats.Add(r.GetString(0));
            }

            if (cats.Count == 0)
                return (new List<Dictionary<string, object?>>(), null, false);

            var afterClause = "";
            if (lastSortKey != null && afterId.HasValue)
                afterClause = "AND (sort_key > @sk OR (sort_key = @sk AND id > @aid))";

            // Build parameters placeholders for IN clause
            var catParams = string.Join(",", cats.Select((_, i) => $"@c{i}"));

            var sql = $@"
                    WITH s AS (
                        SELECT
                            c.id, c.chart_id, c.chart_type, c.vars, c.db_name,
                            ct.title, ct.description, ct.category,
                            md5(concat(@seed, c.id::text)) AS sort_key
                        FROM public.charts c
                        LEFT JOIN public.charts_text ct
                            ON ct.chart_id=c.chart_id AND ct.lang=@lang
                        WHERE EXISTS (
                            SELECT 1 FROM public.charts_text x
                            WHERE x.chart_id=c.chart_id
                            AND x.lang=@lang
                            AND x.category IN ({catParams})
                        )
                    )
                    SELECT * FROM s
                    WHERE 1=1 {afterClause}
                    ORDER BY sort_key ASC, id ASC
                    LIMIT @limit;";

            var rows = await ExecuteReaderAsync(sql, cmd =>
            {
                cmd.Parameters.AddWithValue("seed", seed);
                cmd.Parameters.AddWithValue("lang", lang);
                cmd.Parameters.AddWithValue("limit", limit);

                // Add each category as its own parameter
                for (int i = 0; i < cats.Count; i++)
                    cmd.Parameters.AddWithValue($"c{i}", cats[i]);

                if (lastSortKey != null && afterId.HasValue)
                {
                    cmd.Parameters.AddWithValue("sk", lastSortKey);
                    cmd.Parameters.AddWithValue("aid", afterId.Value);
                }
            }, conn, ct);

            var last = rows.LastOrDefault();
            return (
                rows,
                last == null ? null : new
                {
                    seed,
                    lastSortKey = last["sort_key"]?.ToString(),
                    lastId = Convert.ToInt64(last["id"])
                },
                rows.Count == limit
            );
        }
    }
}
