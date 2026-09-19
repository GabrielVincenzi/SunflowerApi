using SunflowerApi.Models;

namespace SunflowerApi.Repositories
{
    public interface IQuestionnaireRepository
    {
        Task<List<(long QuestionId, int ConsecutiveCorrect)>> GetDueUserQuestionStatesAsync(string userId, int limit, CancellationToken ct);

        Task<List<QuestionDto>> GetQuestionsByIdsAsync(IEnumerable<long> ids, CancellationToken ct);

        Task<List<QuestionDto>> GetFallbackQuestionsFromRecentSeenAsync(string userId, IEnumerable<long> excludedIds, int limit, CancellationToken ct);

        Task<Dictionary<long, List<ChoiceDto>>> GetChoicesByQuestionIdsAsync(IEnumerable<long> qIds, CancellationToken ct);

        Task<QuestionDto?> GetRandomActiveQuestionWithChoicesAsync(CancellationToken ct);

        Task<int> UpsertUserQuestionAsync(string userId, long questionId, DateTime nextDueAt, int consecutiveCorrect, CancellationToken ct);
    }
}
