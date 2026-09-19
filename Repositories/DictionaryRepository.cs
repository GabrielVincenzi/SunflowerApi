using Microsoft.EntityFrameworkCore;
using SunflowerApi.Data;
using SunflowerApi.Models;

namespace SunflowerApi.Repositories
{
    public class DictionaryRepository : IDictionaryRepository
    {
        private readonly TranslationDbContext _context;
        private readonly DictionaryDbContext _dictionaryDbContext;

        public DictionaryRepository(TranslationDbContext context, DictionaryDbContext dictionaryDbContext)
        {
            _context = context;
            _dictionaryDbContext = dictionaryDbContext;
        }

        public async Task<Translation?> GetByLangAsync(string lang, CancellationToken ct)
        {
            return await _context.Translations
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Lang == lang, ct);
        }

        public async Task<Dictionary<string, string>> GetVariableLabelsAsync(
        string table,
        string lang,
        IEnumerable<string> codes,
        CancellationToken ct)
        {
            var distinctCodes = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToArray();

            if (distinctCodes.Length == 0)
                return new Dictionary<string, string>();

            return await _dictionaryDbContext.Set<ColumnLabels>()
                .AsNoTracking()
                .Where(d => d.TableName == table
                         && d.Lang == lang
                         && distinctCodes.Contains(d.ColumnCode))
                .ToDictionaryAsync(d => d.ColumnCode, d => d.Text, ct);
        }
    }
}
