using SunflowerApi.Data;
using SunflowerApi.Repositories;

namespace SunflowerApi.Services
{
    public class ChartService : IChartService
    {
        private readonly IDynamicQueryService _dynamicQueryService;
        private readonly IChartRepository _chartRepository;
        private readonly IDictionaryRepository _dictionaryRepository;
        private readonly ILogger<ChartService> _logger;

        public ChartService(
            IDynamicQueryService dynamicQueryService,
            IChartRepository chartRepository,
            IDictionaryRepository dictionaryRepository,
            ILogger<ChartService> logger)
        {
            _dynamicQueryService = dynamicQueryService;
            _chartRepository = chartRepository;
            _dictionaryRepository = dictionaryRepository;
            _logger = logger;
        }

        public async Task<object> GetChartDataAsync(
            string table,
            string[]? geos,
            string[]? variables,
            DateTime? start,
            DateTime? end,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(table))
                throw new ArgumentException("Table name is required.", nameof(table));

            geos ??= Array.Empty<string>();
            variables ??= Array.Empty<string>();

            DateTime? startNaive = start.HasValue
            ? DateTime.SpecifyKind(start.Value, DateTimeKind.Unspecified)
            : null;

            DateTime? endNaive = end.HasValue
                ? DateTime.SpecifyKind(end.Value, DateTimeKind.Unspecified)
                : null;

            var rows = await _dynamicQueryService.QueryTableAsync(
                table,
                geos.Length > 0 ? geos : null,
                startNaive,
                endNaive,
                ct);

            // Resolve active geos
            var activeGeos = geos.Length > 0
                ? geos
                : rows
                    .Select(r => r.GetValueOrDefault("geo")?.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToArray()!;

            // Resolve active periods
            var activePeriods = rows
                .Select(r => r.GetValueOrDefault("time_period"))
                .Where(v => v is DateTime)
                .Cast<DateTime>()
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var series = new Dictionary<string, List<Dictionary<string, double?>>>();

            foreach (var variable in variables)
            {
                foreach (var geo in activeGeos)
                {
                    var valuesByDate = rows
                        .Where(r => string.Equals(r.GetValueOrDefault("geo")?.ToString(), geo, StringComparison.Ordinal))
                        .Where(r => r.GetValueOrDefault("time_period") is DateTime)
                        .ToDictionary(
                            r => (DateTime)r["time_period"]!,
                            r => ToNullableDouble(r.GetValueOrDefault(variable))
                        );

                    var points = new List<Dictionary<string, double?>>(activePeriods.Count);

                    foreach (var period in activePeriods)
                    {
                        points.Add(new Dictionary<string, double?>
                        {
                            ["value"] = valuesByDate.TryGetValue(period, out var v) ? v : null
                        });
                    }

                    series[$"{variable}_{geo}"] = points;
                }
            }

            return new
            {
                activeGeos = activeGeos.OrderBy(g => g).ToList(),
                activePeriods,
                series
            };
        }

        public async Task<(List<Dictionary<string, object?>> Rows, object? NextCursor, bool HasMore)> GetSelectedChartsAsync(
            string? category,
            string? search,
            int limit,
            long? afterId,
            string lang,
            CancellationToken ct)
        {
            return await _chartRepository.GetSelectedChartsAsync(
                category, search, limit, afterId, lang, ct);
        }

        public async Task<(List<Dictionary<string, object?>> Rows, object? NextCursor, bool HasMore)> GetRecommendedChartsAsync(
            string userId,
            int limit,
            int excludeSeenDays,
            string lang,
            double? lastSimilarity,
            long? afterId,
            CancellationToken ct)
        {
            return await _chartRepository.GetRecommendedChartsAsync(
                userId, limit, excludeSeenDays, lang, lastSimilarity, afterId, ct);
        }

        public async Task<(List<Dictionary<string, object?>> Rows, object? NextCursor, bool HasMore)> GetRandomChartsAsync(
            int limit,
            int categories,
            string lang,
            string? seed,
            string? lastSortKey,
            long? afterId,
            CancellationToken ct)
        {
            return await _chartRepository.GetRandomChartsAsync(
                limit, categories, lang, seed, lastSortKey, afterId, ct);
        }

        private static double? ToNullableDouble(object? value)
        {
            if (value == null || value == DBNull.Value)
                return null;

            var result = value switch
            {
                double d => d,
                float f => (double)f,
                decimal d => (double)d,
                int i => i,
                long l => l,
                _ => Convert.ToDouble(value)
            };

            return double.IsNaN(result) || double.IsInfinity(result)
                ? null
                : result;
        }
    }
}
