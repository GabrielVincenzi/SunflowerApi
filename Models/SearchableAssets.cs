using Pgvector;                  // The Vector type lives here (from the Pgvector NuGet package)
using System.Text.Json;

namespace SunflowerApi.Models;

/// <summary>
/// One row = one searchable piece of content, regardless of its original type.
/// Charts, reports, KPIs, dashboards all write into this table at index time.
/// The search pipeline only ever reads from here — it never touches the original tables.
/// </summary>
public class SearchableAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AssetType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BodyText { get; set; }

    /// <summary>
    /// The 384-dimensional float vector produced by AllMiniLML6v2Sharp.
    /// Pgvector stores and indexes this for cosine similarity search.
    /// </summary>
    public Vector? Embedding { get; set; }

    /// <summary>
    /// How many times this asset has been opened. Used in re-ranking to
    /// boost popular content. Increment this asynchronously — never in the search path.
    /// </summary>
    public int Popularity { get; set; } = 0;

    /// <summary>
    /// Whether this asset should always rank first regardless of score.
    /// Useful for promoted charts, official reports, etc.
    /// </summary>
    public bool IsPinned { get; set; } = false;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}