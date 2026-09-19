using SunflowerApi.Models;

namespace SunflowerApi.Repositories
{
    public interface IUserEventRepository
    {
        Task<List<Guid>> GetSavedEventIdsAsync(
            string userId,
            CancellationToken ct = default);

        Task<List<(Guid ChartId, DateTime SavedAt)>> GetSavedAsync(
            string userId,
            CancellationToken ct = default);

        Task<Dictionary<Guid, Dictionary<string, object?>>> GetChartsByIdsAsync(
            Guid[] ids,
            string lang,
            CancellationToken ct = default);

        Task<int> InsertEventAsync(
            string userId,
            string action,
            Guid objectId,
            CancellationToken ct = default);

        Task<int> InsertSavedAsync(
            string userId,
            Guid objectId,
            CancellationToken ct = default);

        Task<int> DeleteSavedAsync(
            string userId,
            Guid objectId,
            CancellationToken ct = default);

        Task<List<UserEvent>> GetEventsByFilterAsync(
            string userId,
            string action,
            Guid? objectId,
            DateTime? from,
            DateTime? to,
            CancellationToken ct);
    }
}