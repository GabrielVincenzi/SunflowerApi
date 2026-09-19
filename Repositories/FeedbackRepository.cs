using Microsoft.EntityFrameworkCore;
using SunflowerApi.Data;
using SunflowerApi.Models;

namespace SunflowerApi.Repositories
{
    public class FeedbackRepository : IFeedbackRepository
    {
        private readonly FeedbackDbContext _context;

        public FeedbackRepository(FeedbackDbContext context)
        {
            _context = context;
        }

        public async Task<List<FeedbackItem>> GetAllAsync(CancellationToken ct)
        {
            return await _context.FeedbackItems
                .AsNoTracking()
                .Include(f => f.Votes)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<FeedbackItem?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            return await _context.FeedbackItems
                .AsNoTracking()
                .Include(f => f.Votes)
                .FirstOrDefaultAsync(f => f.Id == id, ct);
        }

        public async Task<FeedbackItem> AddAsync(FeedbackItem item, CancellationToken ct)
        {
            _context.FeedbackItems.Add(item);
            await _context.SaveChangesAsync(ct);
            return item;
        }

        public async Task<FeedbackVote?> GetVoteAsync(Guid feedbackItemId, string userId, CancellationToken ct)
        {
            return await _context.FeedbackVotes
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.FeedbackItemId == feedbackItemId && v.UserId == userId, ct);
        }

        public async Task UpsertVoteAsync(FeedbackVote vote, CancellationToken ct)
        {
            var existing = await _context.FeedbackVotes
                .FirstOrDefaultAsync(v => v.FeedbackItemId == vote.FeedbackItemId && v.UserId == vote.UserId, ct);

            if (existing is null)
            {
                _context.FeedbackVotes.Add(vote);
            }
            else
            {
                existing.Direction = vote.Direction;
            }

            await _context.SaveChangesAsync(ct);
        }

        public async Task RemoveVoteAsync(Guid feedbackItemId, string userId, CancellationToken ct)
        {
            var existing = await _context.FeedbackVotes
                .FirstOrDefaultAsync(v => v.FeedbackItemId == feedbackItemId && v.UserId == userId, ct);

            if (existing is not null)
            {
                _context.FeedbackVotes.Remove(existing);
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}