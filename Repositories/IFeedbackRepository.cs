using SunflowerApi.Models;

namespace SunflowerApi.Repositories
{
    public interface IFeedbackRepository
    {
        Task<List<FeedbackItem>> GetAllAsync(CancellationToken ct);
        Task<FeedbackItem?> GetByIdAsync(Guid id, CancellationToken ct);
        Task<FeedbackItem> AddAsync(FeedbackItem item, CancellationToken ct);

        Task<FeedbackVote?> GetVoteAsync(Guid feedbackItemId, string userId, CancellationToken ct);
        Task UpsertVoteAsync(FeedbackVote vote, CancellationToken ct);
        Task RemoveVoteAsync(Guid feedbackItemId, string userId, CancellationToken ct);
    }
}