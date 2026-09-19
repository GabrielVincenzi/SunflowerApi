using Microsoft.EntityFrameworkCore;
using SunflowerApi.Data;
using SunflowerApi.Models;

namespace SunflowerApi.Repositories
{
    public class QuestionnaireRepository : IQuestionnaireRepository
    {
        private readonly QuestionnaireDbContext _db;
        private readonly UserEventsDbContext _eventsDb;

        public QuestionnaireRepository(
            QuestionnaireDbContext db,
            UserEventsDbContext eventsDb)
        {
            _db = db;
            _eventsDb = eventsDb;
        }

        // Test GetQuestionOfTheDay
        // INSERT INTO events (user_id, action, object_id)
        // VALUES ('test-user-123', 'seen', '11111111-1111-1111-1111-111111111111');
        // INSERT INTO questions (id, object_id, title, body, is_active)
        // VALUES (
        //      1
        //     '11111111-1111-1111-1111-111111111111',
        //     'Test question',
        //     'Test body',
        //     true
        // );

        // INSERT INTO choices (question_id, content, is_correct)
        // VALUES
        //     (1, 'Blue', true),
        //     (1, 'Green', false),
        //     (1, 'Red', false);

        //INSERT INTO public.userquestionstates
        //(user_id, question_id, next_due_at, consecutive_correct)
        //VALUES('test-user-123', 1, NOW(), 0);

        public async Task<List<(long QuestionId, int ConsecutiveCorrect)>> GetDueUserQuestionStatesAsync(string userId, int limit, CancellationToken ct)
        {
            // read overdue rows ordered by next_due_at and limit
            var rows = await _db.UserQuestionStates
                .AsNoTracking()
                .Where(u => u.UserId == userId && u.NextDueAt <= DateTime.UtcNow)
                .OrderBy(u => u.NextDueAt)
                .Take(limit)
                .Select(u => new { u.QuestionId, u.ConsecutiveCorrect })
                .ToListAsync(ct);

            return rows.Select(r => (r.QuestionId, r.ConsecutiveCorrect)).ToList();
        }

        public async Task<List<QuestionDto>> GetQuestionsByIdsAsync(IEnumerable<long> ids, CancellationToken ct)
        {
            var idList = ids.ToArray();
            if (idList.Length == 0) return new List<QuestionDto>();

            var questions = await _db.Questions
                .AsNoTracking()
                .Where(q => idList.Contains(q.Id))
                .Select(q => new QuestionDto
                {
                    Id = q.Id,
                    ObjectId = q.ObjectId,
                    Title = q.Title,
                    Body = q.Body,
                    Explanation = q.Explanation,
                    Difficulty = q.Difficulty,
                    Sponsor = q.Sponsor,
                    SponsorBody = q.SponsorBody,
                    SponsorLink = q.SponsorLink
                })
                .ToListAsync(ct);

            // preserve requested order
            var byId = questions.ToDictionary(q => q.Id);
            var ordered = idList.Where(i => byId.ContainsKey(i)).Select(i => byId[i]).ToList();
            return ordered;
        }

        public async Task<List<QuestionDto>> GetFallbackQuestionsFromRecentSeenAsync(string userId, IEnumerable<long> excludedIds, int limit, CancellationToken ct)
        {
            var excluded = excludedIds?.ToArray() ?? Array.Empty<long>();

            // Find object_ids from recent 'seen' events (7 days)
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

            var objectIds = await _eventsDb.UserEvents
                .AsNoTracking()
                .Where(e =>
                    e.UserId == userId &&
                    e.Action == "seen" &&
                    e.Timestamp >= sevenDaysAgo)
                .Select(e => e.ObjectId)
                .Distinct()
                .ToListAsync(ct);

            if (objectIds.Count == 0)
                return new List<QuestionDto>();

            var query = _db.Questions
                .AsNoTracking()
                .Where(q => q.IsActive && objectIds.Contains(q.ObjectId));

            if (excluded.Length > 0)
                query = query.Where(q => !excluded.Contains(q.Id));

            var questions = await query
                .OrderBy(q => q.Id)
                .Take(limit)
                .Select(q => new QuestionDto
                {
                    Id = q.Id,
                    ObjectId = q.ObjectId,
                    Title = q.Title,
                    Body = q.Body,
                    Explanation = q.Explanation,
                    Difficulty = q.Difficulty,
                    Sponsor = q.Sponsor,
                    SponsorBody = q.SponsorBody,
                    SponsorLink = q.SponsorLink
                })
                .ToListAsync(ct);

            return questions;
        }

        public async Task<Dictionary<long, List<ChoiceDto>>> GetChoicesByQuestionIdsAsync(IEnumerable<long> qIds, CancellationToken ct)
        {
            var ids = qIds.ToArray();
            if (ids.Length == 0)
                return new Dictionary<long, List<ChoiceDto>>();

            var choices = await _db.Choices
                .AsNoTracking()
                .Where(c => ids.Contains(c.QuestionId))
                .OrderBy(c => c.QuestionId).ThenBy(c => c.Id)
                .Select(c => new
                {
                    c.Id,
                    c.QuestionId,
                    c.Content,
                    c.IsCorrect
                })
                .ToListAsync(ct);

            var dict = new Dictionary<long, List<ChoiceDto>>();
            foreach (var c in choices)
            {
                if (!dict.TryGetValue(c.QuestionId, out var list))
                {
                    list = new List<ChoiceDto>();
                    dict[c.QuestionId] = list;
                }

                list.Add(new ChoiceDto
                {
                    Id = c.Id,
                    Content = c.Content,
                    IsCorrect = c.IsCorrect
                });
            }

            return dict;
        }

        public async Task<QuestionDto?> GetRandomActiveQuestionWithChoicesAsync(CancellationToken ct)
        {
            // Using EF to pick a random row with PostgreSQL RANDOM() via FromSqlRaw
            var sql = """
                SELECT id, object_id, title, body, explanation, difficulty, sponsor, sponsor_body, sponsor_link
                FROM public.questions
                WHERE is_active = TRUE
                ORDER BY RANDOM()
                LIMIT 1
            """;

            var q = await _db.Questions
                .FromSqlRaw(sql)
                .AsNoTracking()
                .Select(x => new QuestionDto
                {
                    Id = x.Id,
                    ObjectId = x.ObjectId,
                    Title = x.Title,
                    Body = x.Body,
                    Explanation = x.Explanation,
                    Difficulty = x.Difficulty,
                    Sponsor = x.Sponsor,
                    SponsorBody = x.SponsorBody,
                    SponsorLink = x.SponsorLink
                })
                .FirstOrDefaultAsync(ct);

            if (q == null) return null;

            var choicesDict = await GetChoicesByQuestionIdsAsync(new[] { q.Id }, ct);
            q.Choices = choicesDict.TryGetValue(q.Id, out var list) ? list : new List<ChoiceDto>();

            return q;
        }


        public async Task<int> UpsertUserQuestionAsync(
            string userId,
            long questionId,
            DateTime nextDueAt,
            int consecutiveCorrect,
            CancellationToken ct)
        {
            var existing = await _db.UserQuestionStates
                .FirstOrDefaultAsync(u => u.UserId == userId && u.QuestionId == questionId, ct);

            if (existing != null)
            {
                // update existing row
                existing.NextDueAt = nextDueAt;
                existing.ConsecutiveCorrect = consecutiveCorrect;
                _db.UserQuestionStates.Update(existing);
            }
            else
            {
                // create new row
                var newState = new UserQuestionState
                {
                    UserId = userId,
                    QuestionId = questionId,
                    NextDueAt = nextDueAt,
                    ConsecutiveCorrect = consecutiveCorrect
                };

                await _db.UserQuestionStates.AddAsync(newState, ct);
            }

            return await _db.SaveChangesAsync(ct);
        }
    }
}
