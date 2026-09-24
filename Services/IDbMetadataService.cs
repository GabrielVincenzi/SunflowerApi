using SunflowerApi.Models;

namespace SunflowerApi.Services
{
    public interface IDbMetadataService
    {
        Task<DbMetadata?> GetDbMetadataAsync(string name, CancellationToken ct);
        Task<List<FilterOptionDto>> GetCategoriesAsync(string lang, CancellationToken ct);
        Task<List<FilterOptionDto>> GetSourcesAsync(string lang, CancellationToken ct);
    }
}