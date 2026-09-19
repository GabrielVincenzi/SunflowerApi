using SunflowerApi.Models;

namespace SunflowerApi.Services
{
    public interface IUserEventService
    {
        Task<object> GetSavedEventIdsAsync(string userId, CancellationToken ct);
        Task<object> GetSavedEventsAsync(string userId, string lang, CancellationToken ct);
        Task<int> CreateEventAsync(string userId, string Action, Guid objectId, CancellationToken ct);
        Task<int> DeleteSavedAsync(string userId, Guid objectId, CancellationToken ct);
        Task<List<UserEvent>> GetFilteredEventsAsync(
            string userId,
            string action,
            Guid? objectId,
            DateTime? from,
            DateTime? to,
            CancellationToken ct);
    }
}
