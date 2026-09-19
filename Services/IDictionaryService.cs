namespace SunflowerApi.Services
{
    public interface IDictionaryService
    {
        Task<object?> GetMobilePagesAsync(string? lang, CancellationToken ct);
    }
}
