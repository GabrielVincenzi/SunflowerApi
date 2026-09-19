using SunflowerApi.Models;

namespace SunflowerApi.Services
{
    public interface IDbMetadataService
    {
        Task<DbMetadata?> GetDbMetadataAsync(string name, CancellationToken ct);
        Task<List<CategoryDto>> GetCategoriesAsync(string lang, CancellationToken ct);
    }
}