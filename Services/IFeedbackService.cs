using SunflowerApi.Models;

namespace SunflowerApi.Services
{
    public interface IFeedbackService
    {
        Task<List<FeedbackItemDto>> GetAllAsync(string? currentUserId, CancellationToken ct);
        Task<FeedbackItemDto> CreateAsync(string userId, string? authorName, string message, CancellationToken ct);
        Task<FeedbackItemDto?> VoteAsync(Guid feedbackItemId, string userId, string direction, CancellationToken ct);
    }
}