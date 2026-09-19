using SunflowerApi.Models;
using SunflowerApi.Repositories;

namespace SunflowerApi.Services
{
    public class FeedbackService : IFeedbackService
    {
        private readonly IFeedbackRepository _repository;

        public FeedbackService(IFeedbackRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<FeedbackItemDto>> GetAllAsync(string? currentUserId, CancellationToken ct)
        {
            var items = await _repository.GetAllAsync(ct);
            return items.Select(item => ToDto(item, currentUserId)).ToList();
        }

        public async Task<FeedbackItemDto> CreateAsync(string userId, string? authorName, string message, CancellationToken ct)
        {
            var item = new FeedbackItem
            {
                Message = message.Trim(),
                AuthorId = userId,
                AuthorName = authorName,
            };

            var created = await _repository.AddAsync(item, ct);
            return ToDto(created, userId);
        }

        public async Task<FeedbackItemDto?> VoteAsync(Guid feedbackItemId, string userId, string direction, CancellationToken ct)
        {
            var item = await _repository.GetByIdAsync(feedbackItemId, ct);
            if (item is null) return null;

            var requestedDirection = direction == "up" ? VoteDirection.Up : VoteDirection.Down;
            var existingVote = await _repository.GetVoteAsync(feedbackItemId, userId, ct);

            if (existingVote is not null && existingVote.Direction == requestedDirection)
            {
                // tapping the same arrow again retracts the vote
                await _repository.RemoveVoteAsync(feedbackItemId, userId, ct);
            }
            else
            {
                // fresh vote, or switching from the opposite direction
                await _repository.UpsertVoteAsync(new FeedbackVote
                {
                    FeedbackItemId = feedbackItemId,
                    UserId = userId,
                    Direction = requestedDirection,
                }, ct);
            }

            var updated = await _repository.GetByIdAsync(feedbackItemId, ct);
            return ToDto(updated!, userId);
        }

        private static FeedbackItemDto ToDto(FeedbackItem item, string? currentUserId)
        {
            var upvotes = item.Votes.Count(v => v.Direction == VoteDirection.Up);
            var downvotes = item.Votes.Count(v => v.Direction == VoteDirection.Down);

            string? myVote = null;
            if (currentUserId is not null)
            {
                var mine = item.Votes.FirstOrDefault(v => v.UserId == currentUserId);
                myVote = mine?.Direction switch
                {
                    VoteDirection.Up => "up",
                    VoteDirection.Down => "down",
                    _ => null,
                };
            }

            return new FeedbackItemDto(
                item.Id,
                item.Message,
                item.AuthorName,
                upvotes,
                downvotes,
                upvotes - downvotes,
                myVote,
                item.CreatedAt
            );
        }
    }
}