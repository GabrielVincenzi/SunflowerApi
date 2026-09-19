using SunflowerApi.Models;

namespace SunflowerApi.Repositories
{
    public interface IDictionaryRepository
    {
        Task<Translation?> GetByLangAsync(string lang, CancellationToken ct);
        Task<Dictionary<string, string>> GetVariableLabelsAsync(
            string table,
            string lang,
            IEnumerable<string> codes,
            CancellationToken ct = default);
    }
}
