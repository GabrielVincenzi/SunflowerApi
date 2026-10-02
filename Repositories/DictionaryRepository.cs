using Microsoft.EntityFrameworkCore;
using SunflowerApi.Data;
using SunflowerApi.Models;
using System.Text.Json;

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

        public async Task<Dictionary<string, object?>> AttachLabelsAsync(
            JsonElement varsElement, string source, string lang, CancellationToken ct)
        {
            var roots = CodeParser.ParseVars(varsElement);

            // Pass 1: every (codelist, code) needed for this chart, root codes included.
            var needed = new HashSet<(string codelist, string code)>();
            foreach (var r in roots) CodeParser.CollectCodes(r, needed);

            // One query per codelist, same lookup as before.
            var byCodelist = needed
                .GroupBy(n => n.codelist)
                .ToDictionary(g => g.Key, g => g.Select(n => n.code).Distinct().ToList());

            var resolved = new Dictionary<(string, string), string>();
            foreach (var (codelist, codes) in byCodelist)
            {
                var codelistUpper = codelist.ToUpperInvariant();
                var sourceUpper = source.ToUpperInvariant();
                var codesUpper = codes.Select(c => c.ToUpperInvariant()).Distinct().ToList();

                var rows = await _dictionaryDbContext.Set<DictionaryEntry>()
                    .AsNoTracking()
                    .Where(d => d.SourceInst == sourceUpper && d.Codelist == codelistUpper
                                && d.Lang == lang && codesUpper.Contains(d.Code))
                    .ToListAsync(ct);

                // map back from the stored uppercase code to the code as written in vars
                var originals = codes.GroupBy(c => c.ToUpperInvariant())
                                     .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var r in rows)
                    if (originals.TryGetValue(r.Code, out var list))
                        foreach (var original in list)
                            resolved[(codelist, original)] = r.Text;
            }

            // Pass 2: vars = codes only (pattern dropped), labels = mirrored tree.
            var varsOut = new Dictionary<string, object?>();
            var labelsOut = new Dictionary<string, object?>();
            foreach (var r in roots)
            {
                varsOut[r.Root] = r.Values;
                labelsOut[r.Root] = CodeParser.BuildLabels(r, resolved);   // [rootLabel, labelTree]
            }

            return new() { ["vars"] = varsOut, ["labels"] = labelsOut };
        }
    }
}
