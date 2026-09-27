using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using SunflowerApi.Data;
using SunflowerApi.Models;

namespace SunflowerApi.Repositories
{
    public class DbMetadataRepository : IDbMetadataRepository
    {
        private readonly DbMetadataDbContext _metadataContext;
        private readonly FilterDbContext _categoryContext;

        public DbMetadataRepository(
            DbMetadataDbContext metadataContext,
            FilterDbContext categoryContext)
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

        public Task<List<FilterOptionDto>> GetCategoriesAsync(string lang, CancellationToken ct)
        => GetOptionsAsync<Category>(lang, ct);

        public Task<List<FilterOptionDto>> GetSourcesAsync(string lang, CancellationToken ct)
            => GetOptionsAsync<Source>(lang, ct);

        private Task<List<FilterOptionDto>> GetOptionsAsync<T>(string lang, CancellationToken ct)
            where T : class, ILocalizedOption
            => _categoryContext.Set<T>()
                .AsNoTracking()
                .Where(o => o.Lang == lang && o.Description != null)
                .OrderBy(o => o.Description)
                .Select(o => new FilterOptionDto(o.Name, o.Description!))
                .ToListAsync(ct);
    }
}
