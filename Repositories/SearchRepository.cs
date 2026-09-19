using System.Globalization;
using Npgsql;

namespace SunflowerApi.Repositories;

// ─────────────────────────────────────────────────────────────────────────
// RECOMMENDED INDEXES (verify exact syntax against your Postgres/CockroachDB
// version before applying — CockroachDB's vector index / ANN support and
// available operator classes differ by version):
//
//   CREATE INDEX IF NOT EXISTS idx_charts_text_lookup
//       ON public.charts_text (chart_id, lang, text_category);
//
//   CREATE INDEX IF NOT EXISTS idx_charts_vector_ann
//       ON public.charts USING hnsw (vector_dim vector_cosine_ops); -- pgvector HNSW
//       -- (CockroachDB: check current docs for its native vector index syntax)
//
//   CREATE INDEX IF NOT EXISTS idx_charts_id ON public.charts (id); -- keyset browse
//
// Without an ANN index the semantic/fused branches do a full sequential scan +
// exact cosine distance per row. That's fine for a catalog of hundreds/low
// thousands of charts (which is the Sunflower scale today) but will not scale
// to a large corpus — revisit if the charts table grows substantially.
// ─────────────────────────────────────────────────────────────────────────

public class SearchRepository : ISearchRepository
{
    private readonly string _connStr;

    // RRF constant. 60 is the de-facto standard from the original RRF paper /
    // most hybrid-search implementations (Postgres, Elastic, Weaviate all use it).
    // Lower it to weight top-ranked results more heavily; raise it to flatten
    // the influence of rank position.
    private const int RrfK = 60;

    // Same threshold you were already using — cosine similarities below this
    // are treated as "not a semantic match" rather than included with a low score.
    private const double SemanticSimilarityThreshold = 0.4;

    public SearchRepository(IConfiguration config)
    {
        _connStr = config.GetConnectionString("DefaultConnection")!;
    }

    public async Task<(List<Dictionary<string, object?>> Rows, long? NextCursor, bool HasMore)>
        HybridSearchChartsAsync(
            string? search,
            float[]? vector,
            string? category,
            string? source,
            string lang,
            int limit,
            long? afterCursor,
            CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var hasVector = vector is { Length: > 0 };

        if (!hasSearch && !hasVector)
            return await BrowseChartsAsync(category, source, lang, limit, afterCursor, ct);

        if (hasSearch && hasVector)
            return await FusedSearchAsync(search!, vector!, category, source, lang, limit, afterCursor, ct);

        if (hasVector)
            return await SemanticOnlySearchAsync(vector!, category, source, lang, limit, afterCursor, ct);

        return await KeywordOnlySearchAsync(search!, category, source, lang, limit, afterCursor, ct);
    }

    // ─────────────────────────────────────────────────────────────────
    // Mode 1: no search, no vector — plain listing, legacy id-keyset
    // ─────────────────────────────────────────────────────────────────
    private async Task<(List<Dictionary<string, object?>>, long?, bool)> BrowseChartsAsync(
        string? category, string? source, string lang, int limit, long? afterId, CancellationToken ct)
    {
        var fetchCount = limit + 1;

        var sql = @"
            WITH base AS (
                SELECT
                    c.id, c.chart_id, c.chart_type, c.vars, c.db_name,
                    ct.title, ct.description, ct.category,
                    d.db_source AS source
                FROM public.charts c
                LEFT JOIN public.charts_text ct
                    ON ct.chart_id = c.chart_id AND ct.lang = @lang
                LEFT JOIN public.dbs d
                    ON d.db_name = c.db_name
            )
            SELECT * FROM base
            WHERE 1=1";

        if (afterId.HasValue) sql += " AND id > @afterId";
        if (!string.IsNullOrWhiteSpace(category)) sql += " AND category ILIKE @category";
        if (!string.IsNullOrWhiteSpace(source)) sql += " AND LOWER(source) = LOWER(@source)";
        sql += " ORDER BY id ASC LIMIT @limit;";

        var rows = await ExecuteRowsAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue("lang", lang);
            cmd.Parameters.AddWithValue("limit", fetchCount);
            if (afterId.HasValue) cmd.Parameters.AddWithValue("afterId", afterId.Value);
            if (!string.IsNullOrWhiteSpace(category)) cmd.Parameters.AddWithValue("category", $"%{category}%");
        }, ct);

        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);

        long? nextCursor = hasMore && rows.Count > 0 ? Convert.ToInt64(rows[^1]["id"]) : null;
        return (rows, nextCursor, hasMore);
    }

    // ─────────────────────────────────────────────────────────────────
    // Mode 2: search only — ILIKE match required, ranked title > description,
    // offset pagination (cursor is a row offset, opaque to the client)
    // ─────────────────────────────────────────────────────────────────
    private async Task<(List<Dictionary<string, object?>>, long?, bool)> KeywordOnlySearchAsync(
        string search, string? category, string? source, string lang, int limit, long? afterOffset, CancellationToken ct)
    {
        var offset = afterOffset ?? 0;
        var fetchCount = limit + 1;
        var escaped = EscapeLikePattern(search);

        var sql = @"
            WITH base AS (
                SELECT
                    c.id, c.chart_id, c.chart_type, c.vars, c.db_name,
                    ct.title, ct.description, ct.category,
                    d.db_source AS source
                FROM public.charts c
                LEFT JOIN public.charts_text ct
                       ON ct.chart_id = c.chart_id AND ct.lang = @lang
                LEFT JOIN public.dbs d
                    ON d.db_name = c.db_name
            ),
            matches AS (
                SELECT *,
                    CASE
                        WHEN title ILIKE @searchExact ESCAPE '\' THEN 4
                        WHEN title ILIKE @searchPrefix ESCAPE '\' THEN 3
                        WHEN title ILIKE @searchAny ESCAPE '\' THEN 2
                        WHEN description ILIKE @searchAny ESCAPE '\' THEN 1
                        ELSE 0
                    END AS relevance
                FROM base
                WHERE (title ILIKE @searchAny ESCAPE '\' OR description ILIKE @searchAny ESCAPE '\')";

        if (!string.IsNullOrWhiteSpace(category)) sql += " AND category ILIKE @category";
        if (!string.IsNullOrWhiteSpace(source)) sql += " AND LOWER(source) = LOWER(@source)";

        sql += @"
            )
            SELECT * FROM matches
            ORDER BY relevance DESC, id ASC
            LIMIT @limit OFFSET @offset;";

        var rows = await ExecuteRowsAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue("lang", lang);
            cmd.Parameters.AddWithValue("searchExact", escaped);
            cmd.Parameters.AddWithValue("searchPrefix", $"{escaped}%");
            cmd.Parameters.AddWithValue("searchAny", $"%{escaped}%");
            cmd.Parameters.AddWithValue("limit", fetchCount);
            cmd.Parameters.AddWithValue("offset", offset);
            if (!string.IsNullOrWhiteSpace(category)) cmd.Parameters.AddWithValue("category", $"%{category}%");
        }, ct);

        return BuildOffsetPage(rows, limit, offset);
    }

    // ─────────────────────────────────────────────────────────────────
    // Mode 3: vector only — semantic match required, ranked by cosine similarity
    // ─────────────────────────────────────────────────────────────────
    private async Task<(List<Dictionary<string, object?>>, long?, bool)> SemanticOnlySearchAsync(
        float[] vector, string? category, string? source, string lang, int limit, long? afterOffset, CancellationToken ct)
    {
        var offset = afterOffset ?? 0;
        var fetchCount = limit + 1;

        var sql = @"
            WITH base AS (
                SELECT
                    c.id, c.chart_id, c.chart_type, c.vars, c.db_name,
                    1 - (c.vector_dim <=> @vec::vector) AS relevance,
                    ct.title, ct.description, ct.category,
                    d.db_source AS source
                FROM public.charts c
                LEFT JOIN public.charts_text ct
                       ON ct.chart_id = c.chart_id AND ct.lang = @lang
                LEFT JOIN public.dbs d
                    ON d.db_name = c.db_name

                WHERE c.vector_dim IS NOT NULL
            )
            SELECT * FROM base
            WHERE relevance > @simThreshold";

        if (!string.IsNullOrWhiteSpace(category)) sql += " AND category ILIKE @category";
        if (!string.IsNullOrWhiteSpace(source)) sql += " AND LOWER(source) = LOWER(@source)";
        sql += " ORDER BY relevance DESC, id ASC LIMIT @limit OFFSET @offset;";

        var rows = await ExecuteRowsAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue("vec", ToVectorLiteral(vector));
            cmd.Parameters.AddWithValue("lang", lang);
            cmd.Parameters.AddWithValue("simThreshold", SemanticSimilarityThreshold);
            cmd.Parameters.AddWithValue("limit", fetchCount);
            cmd.Parameters.AddWithValue("offset", offset);
            if (!string.IsNullOrWhiteSpace(category)) cmd.Parameters.AddWithValue("category", $"%{category}%");
        }, ct);

        return BuildOffsetPage(rows, limit, offset);
    }

    // ─────────────────────────────────────────────────────────────────
    // Mode 4: search + vector — union of both matches, fused via
    // Reciprocal Rank Fusion so the two incomparable scales (ILIKE relevance
    // vs cosine similarity) combine sensibly without manual normalization
    // ─────────────────────────────────────────────────────────────────
    private async Task<(List<Dictionary<string, object?>>, long?, bool)> FusedSearchAsync(
        string search, float[] vector, string? category, string? source, string lang, int limit, long? afterOffset,
        CancellationToken ct)
    {
        var offset = afterOffset ?? 0;
        var fetchCount = limit + 1;
        var escaped = EscapeLikePattern(search);

        var sql = @"
            WITH base AS (
                SELECT
                    c.id, c.chart_id, c.chart_type, c.vars, c.db_name, c.vector_dim,
                    ct.title, ct.description, ct.category,
                    d.db_source AS source
                FROM public.charts c
                LEFT JOIN public.charts_text ct
                       ON ct.chart_id = c.chart_id AND ct.lang = @lang
                LEFT JOIN public.dbs d
                    ON d.db_name = c.db_name
            ),
            filtered AS (
                SELECT * FROM base WHERE 1=1";

        if (!string.IsNullOrWhiteSpace(category)) sql += " AND category ILIKE @category";
        if (!string.IsNullOrWhiteSpace(source)) sql += " AND LOWER(source) = LOWER(@source)";

        sql += @"
            ),
            text_hits AS (
                SELECT id,
                    CASE
                        WHEN title ILIKE @searchExact ESCAPE '\' THEN 4
                        WHEN title ILIKE @searchPrefix ESCAPE '\' THEN 3
                        WHEN title ILIKE @searchAny ESCAPE '\' THEN 2
                        WHEN description ILIKE @searchAny ESCAPE '\' THEN 1
                        ELSE 0
                    END AS text_score
                FROM filtered
                WHERE title ILIKE @searchAny ESCAPE '\' OR description ILIKE @searchAny ESCAPE '\'
            ),
            text_ranked AS (
                SELECT id, ROW_NUMBER() OVER (ORDER BY text_score DESC, id ASC) AS text_rank
                FROM text_hits
            ),
            semantic_hits AS (
                SELECT id, 1 - (vector_dim <=> @vec::vector) AS similarity
                FROM filtered
                WHERE vector_dim IS NOT NULL
            ),
            semantic_ranked AS (
                SELECT id, ROW_NUMBER() OVER (ORDER BY similarity DESC) AS semantic_rank
                FROM semantic_hits
                WHERE similarity > @simThreshold
            ),
            fused AS (
                SELECT
                    COALESCE(t.id, s.id) AS id,
                    COALESCE(1.0 / (@rrfK + t.text_rank), 0)
                        + COALESCE(1.0 / (@rrfK + s.semantic_rank), 0) AS relevance
                FROM text_ranked t
                FULL OUTER JOIN semantic_ranked s ON t.id = s.id
            )
            SELECT f.id, f.chart_id, f.chart_type, f.vars, f.db_name,
                   f.title, f.description, f.category, fused.relevance
            FROM filtered f
            JOIN fused ON fused.id = f.id
            ORDER BY fused.relevance DESC, f.id ASC
            LIMIT @limit OFFSET @offset;";

        var rows = await ExecuteRowsAsync(sql, cmd =>
        {
            cmd.Parameters.AddWithValue("lang", lang);
            cmd.Parameters.AddWithValue("vec", ToVectorLiteral(vector));
            cmd.Parameters.AddWithValue("simThreshold", SemanticSimilarityThreshold);
            cmd.Parameters.AddWithValue("rrfK", RrfK);
            cmd.Parameters.AddWithValue("searchExact", escaped);
            cmd.Parameters.AddWithValue("searchPrefix", $"{escaped}%");
            cmd.Parameters.AddWithValue("searchAny", $"%{escaped}%");
            cmd.Parameters.AddWithValue("limit", fetchCount);
            cmd.Parameters.AddWithValue("offset", offset);
            if (!string.IsNullOrWhiteSpace(category)) cmd.Parameters.AddWithValue("category", $"%{category}%");
        }, ct);

        return BuildOffsetPage(rows, limit, offset);
    }

    // ─────────────────────────────────────────────────────────────────
    // Shared helpers
    // ─────────────────────────────────────────────────────────────────
    private static (List<Dictionary<string, object?>>, long?, bool) BuildOffsetPage(
        List<Dictionary<string, object?>> rows, int limit, long offset)
    {
        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);

        // Cursor is an opaque row-offset, not an id — see remarks on ISearchRepository.
        long? nextCursor = hasMore ? offset + rows.Count : null;
        return (rows, nextCursor, hasMore);
    }

    private static string ToVectorLiteral(float[] vector) =>
        $"[{string.Join(",", vector.Select(x => x.ToString("G", CultureInfo.InvariantCulture)))}]";

    // Escapes ILIKE wildcard characters in user input so a search for e.g.
    // "50%" or "a_b" is treated literally instead of as a wildcard pattern.
    private static string EscapeLikePattern(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private async Task<List<Dictionary<string, object?>>> ExecuteRowsAsync(
        string sql, Action<NpgsqlCommand> configure, CancellationToken ct)
    {
        var rows = new List<Dictionary<string, object?>>();

        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        configure(cmd);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        return rows;
    }
}