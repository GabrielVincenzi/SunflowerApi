using SunflowerApi.Models;
using SunflowerApi.Repositories;

namespace SunflowerApi.Services
{
    public class DbMetadataService : IDbMetadataService
    {
        private readonly IDbMetadataRepository _repository;

        public DbMetadataService(IDbMetadataRepository repository)
        {
            _repository = repository;
        }

        public Task<DbMetadata?> GetDbMetadataAsync(string name, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("name is required");

            return _repository.GetDbAsync(name, ct);
        }

        public Task<List<CategoryDto>> GetCategoriesAsync(string lang, CancellationToken ct)
        {
            return _repository.GetCategoriesAsync(lang, ct);
        }
    }
}
