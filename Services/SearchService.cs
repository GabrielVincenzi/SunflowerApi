using SunflowerApi.Repositories;

namespace SunflowerApi.Services;

public class SearchService : ISearchService
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    // Xenova/multilingual-e5-small, per the on-device embeddingService.ts —
    // keep this in sync if the embedding model ever changes.
    private const int ExpectedVectorDimension = 384;

    private readonly ISearchRepository _searchRepository;
    private readonly IDictionaryRepository _dictionaryRepository;
    private readonly IDbMetadataRepository _dbMetadataRepository;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        ISearchRepository searchRepository,
        IDictionaryRepository dictionaryRepository,
        IDbMetadataRepository dbMetadataRepository,
        ILogger<SearchService> logger)
    {
        _searchRepository = searchRepository;
        _dictionaryRepository = dictionaryRepository;
        _dbMetadataRepository = dbMetadataRepository;
        _logger = logger;
    }

    public async Task<ChartSearchResult> SearchChartsAsync(ChartSearchQuery query, CancellationToken ct = default)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var lang = string.IsNullOrWhiteSpace(query.Lang) ? "en" : query.Lang.Trim();
        var category = string.IsNullOrWhiteSpace(query.Category) ? null : query.Category.Trim();
        var source = string.IsNullOrWhiteSpace(query.Source) ? null : query.Source.Trim();
        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);

        var vector = ValidateVector(query.Vector);

        List<Dictionary<string, object?>> rows;
        long? nextCursor;
        bool hasMore;

        try
        {
            (rows, nextCursor, hasMore) = await _searchRepository.HybridSearchChartsAsync(
                search, vector, category, source, lang, limit, query.AfterCursor, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Chart search failed. hasSearch={HasSearch} hasVector={HasVector} category={Category} source={Source} lang={Lang}",
                search != null, vector != null, category, source, lang);
            throw;
        }

        return new ChartSearchResult(rows, nextCursor, hasMore, limit);
    }

    private static float[]? ValidateVector(float[]? vector)
    {
        if (vector is null || vector.Length == 0) return null;

        if (vector.Length != ExpectedVectorDimension)
        {
            throw new ArgumentException(
                $"Query vector must have {ExpectedVectorDimension} dimensions, received {vector.Length}.");
        }

        if (vector.Any(x => float.IsNaN(x) || float.IsInfinity(x)))
        {
            throw new ArgumentException("Query vector contains invalid (NaN/Infinity) values.");
        }

        return vector;
    }
}