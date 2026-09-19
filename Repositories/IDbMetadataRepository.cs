using SunflowerApi.Models;

namespace SunflowerApi.Repositories
{
    public interface IDbMetadataRepository
    {
        Task<DbMetadata?> GetDbAsync(string name, CancellationToken ct);
        Task<List<CategoryDto>> GetCategoriesAsync(string lang, CancellationToken ct);
        Task<Dictionary<string, string?>> GetSourcesByDbNamesAsync(IEnumerable<string> dbNames, CancellationToken ct);
    }
}