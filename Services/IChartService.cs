namespace SunflowerApi.Services
{
    public interface IChartService
    {
        Task<object> GetChartDataAsync(
            string table,
            string[] geos,
            string[] variables,
            DateTime? start,
            DateTime? end,
            CancellationToken ct);

        Task<(List<Dictionary<string, object?>> Rows, object? NextCursor, bool HasMore)> GetSelectedChartsAsync(
            string? category,
            string? search,
            int limit,
            long? afterId,
            string lang,
            CancellationToken ct);

        Task<(List<Dictionary<string, object?>> Rows, object? NextCursor, bool HasMore)> GetRecommendedChartsAsync(
            string userId,
            int limit,
            int excludeSeenDays,
            string lang,
            double? lastSimilarity,
            long? afterId,
            CancellationToken ct = default);

        Task<(List<Dictionary<string, object?>> Rows, object? NextCursor, bool HasMore)> GetRandomChartsAsync(
            int limit,
            int categories,
            string lang = "en",
            string? seed = null,
            string? lastSortKey = null,
            long? afterId = null,
            CancellationToken ct = default);
    }
}
