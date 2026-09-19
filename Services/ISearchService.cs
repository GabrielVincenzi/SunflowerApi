public interface ISearchService
{
    Task<ChartSearchResult> SearchChartsAsync(ChartSearchQuery query, CancellationToken ct = default);
}

public sealed record ChartSearchQuery(
    string? Search,
    float[]? Vector,
    string? Category,
    string? Source,
    string? Lang,
    int? Limit,
    long? AfterCursor);

public sealed record ChartSearchResult(
    List<Dictionary<string, object?>> Data,
    long? NextCursor,
    bool HasMore,
    int Limit);