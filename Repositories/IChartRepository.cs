namespace SunflowerApi.Repositories
{
    public interface IChartRepository
    {
        Task<(List<Dictionary<string, object?>> Rows, long? NextCursor, bool HasMore)> GetSelectedChartsAsync(
            string? category,
            string? search,
            int limit,
            long? afterId,
            string lang,
            CancellationToken cancellationToken = default);

        Task<(List<Dictionary<string, object?>> Rows, object? NextCursor, bool HasMore)> GetRecommendedChartsAsync(
            string userId,
            int limit,
            int excludeSeenDays,
            string lang,
            double? lastSimilarity,
            long? afterId,
            CancellationToken cancellationToken = default);

        Task<(List<Dictionary<string, object?>> Rows, object? NextCursor, bool HasMore)> GetRandomChartsAsync(
            int limit,
            int categories,
            string lang,
            string? seed,
            string? lastSortKey,
            long? afterId,
            CancellationToken cancellationToken = default);
    }
}
