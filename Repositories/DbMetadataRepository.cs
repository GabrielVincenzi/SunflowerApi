using Microsoft.EntityFrameworkCore;
using SunflowerApi.Data;
using SunflowerApi.Models;

namespace SunflowerApi.Repositories
{
    public class DbMetadataRepository : IDbMetadataRepository
    {
        private readonly DbMetadataDbContext _metadataContext;
        private readonly CategoryDbContext _categoryContext;

        public DbMetadataRepository(
            DbMetadataDbContext metadataContext,
            CategoryDbContext categoryContext)
        {
            _metadataContext = metadataContext;
            _categoryContext = categoryContext;
        }

        public async Task<DbMetadata?> GetDbAsync(string name, CancellationToken ct)
        {
            return await _metadataContext.DbsMetadata
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DbName == name, ct);
        }

        public async Task<List<CategoryDto>> GetCategoriesAsync(string lang, CancellationToken ct)
        {
            return await _categoryContext.Set<Category>()
                .AsNoTracking()
                .Where(c => c.Description != null)
                .Where(c => c.Lang == lang)
                .OrderBy(c => c.Description)
                .Select(c => new CategoryDto(c.Name, c.Description!))
                .ToListAsync(ct);
        }

        public async Task<Dictionary<string, string?>> GetSourcesByDbNamesAsync(
    IEnumerable<string> dbNames, CancellationToken ct)
        {
            var names = dbNames.Distinct().ToArray();
            if (names.Length == 0) return new Dictionary<string, string?>();

            return await _metadataContext.DbsMetadata
                .AsNoTracking()
                .Where(d => names.Contains(d.DbName))
                .ToDictionaryAsync(d => d.DbName, d => d.DbSource, ct);
        }
    }
}
