namespace SunflowerApi.Repositories;

public interface ISearchRepository
{
    /// <summary>
    /// Hybrid search entry point. Internally dispatches to one of four strategies
    /// based on which inputs are present:
    ///   - neither search nor vector -> plain browse, id-keyset pagination
    ///   - search only               -> keyword search, offset pagination, ranked title > description
    ///   - vector only               -> semantic search, offset pagination, ranked by cosine similarity
    ///   - both                      -> fused search, offset pagination, ranked by Reciprocal Rank Fusion
    ///
    /// NOTE ON PAGINATION: for the three ranked modes, "afterCursor" / "nextCursor" is an
    /// opaque row-offset rather than an id. This is safe because the client (see
    /// fetchAllChartDetails) never inspects the cursor value itself — it only ever echoes
    /// back whatever nextCursor the server last returned.
    /// </summary>
    Task<(List<Dictionary<string, object?>> Rows, long? NextCursor, bool HasMore)> HybridSearchChartsAsync(
        string? search,
        float[]? vector,
        string? category,
        string? source,
        string lang,
        int limit,
        long? afterCursor,
        CancellationToken ct = default);
}