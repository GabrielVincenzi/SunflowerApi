using SunflowerApi.Repositories;
using SunflowerApi.Models;

namespace SunflowerApi.Services
{
    public class UserEventService : IUserEventService
    {
        private readonly IUserEventRepository _repo;

        public UserEventService(IUserEventRepository repo)
        {
            _repo = repo;
        }

        public async Task<object> GetSavedEventIdsAsync(string userId, CancellationToken ct)
        {
            var ids = await _repo.GetSavedEventIdsAsync(userId, ct);
            return new { ids };
        }

        public async Task<object> GetSavedEventsAsync(string userId, string lang, CancellationToken ct)
        {
            var saved = await _repo.GetSavedAsync(userId, ct);
            if (saved.Count == 0)
                return new { data = Array.Empty<object>() };

            var charts = await _repo.GetChartsByIdsAsync(
                saved.Select(x => x.ChartId).ToArray(),
                lang,
                ct);

            var result = saved
                .Where(x => charts.ContainsKey(x.ChartId))
                .Select(x => new
                {
                    savedAt = x.SavedAt,
                    data = charts[x.ChartId]
                });

            return new { data = result };
        }

        public async Task<int> CreateEventAsync(string userId, string Action, Guid objectId, CancellationToken ct)
        {
            var action = Action!.Trim().ToLowerInvariant();

            return action == "saved"
                ? await _repo.InsertSavedAsync(userId, objectId, ct)
                : await _repo.InsertEventAsync(userId, action, objectId, ct);
        }

        public Task<int> DeleteSavedAsync(string userId, Guid objectId, CancellationToken ct)
            => _repo.DeleteSavedAsync(userId, objectId, ct);

        public async Task<List<UserEvent>> GetFilteredEventsAsync(
            string userId,
            string action,
            Guid? objectId,
            DateTime? from,
            DateTime? to,
            CancellationToken ct)
        {
            var normalisedAction = action.Trim().ToLowerInvariant();

            return await _repo.GetEventsByFilterAsync(userId, normalisedAction, objectId, from, to, ct);
        }
    }
}