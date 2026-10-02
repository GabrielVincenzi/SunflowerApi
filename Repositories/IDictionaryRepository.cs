using SunflowerApi.Models;
using System.Text.Json;

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

        Task<Dictionary<string, object?>> AttachLabelsAsync(JsonElement varsElement, string source, string lang, CancellationToken ct);
    }
}
