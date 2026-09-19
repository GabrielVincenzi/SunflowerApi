using SunflowerApi.Models;

namespace SunflowerApi.Services
{
    public interface IQuestionnaireService
    {
        Task<object> GetQuestionOfTheDayAsync(string userId, int numQuestions, CancellationToken ct);
        Task<object> GetRandomQuestionAsync(CancellationToken ct);
        Task<int> UpsertUserQuestionAsync(UserQuestionState request, CancellationToken ct);
    }
}
