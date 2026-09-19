using System.Text.Json;
using SunflowerApi.Repositories;

namespace SunflowerApi.Services
{
    public class DictionaryService : IDictionaryService
    {
        private readonly IDictionaryRepository _repository;

        public DictionaryService(IDictionaryRepository repository)
        {
            _repository = repository;
        }

        public async Task<object?> GetMobilePagesAsync(string? lang, CancellationToken ct)
        {
            var language = string.IsNullOrWhiteSpace(lang) ? "en" : lang;

            var translation = await _repository.GetByLangAsync(language, ct);
            if (translation is null)
                return null;

            var payloadJson = JsonDocument
                .Parse(translation.Payload)
                .RootElement;

            return new
            {
                version = translation.Version.ToString("O"),
                payload = payloadJson
            };
        }
    }
}
