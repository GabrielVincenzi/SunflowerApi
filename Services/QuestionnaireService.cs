using SunflowerApi.Repositories;
using SunflowerApi.Models;

namespace SunflowerApi.Services
{
    public class QuestionnaireService : IQuestionnaireService
    {
        private readonly IQuestionnaireRepository _repo;

        public QuestionnaireService(IQuestionnaireRepository repo)
        {
            _repo = repo;
        }

        public async Task<object> GetQuestionOfTheDayAsync(
            string userId,
            int numQuestions,
            CancellationToken ct)
        {
            if (numQuestions <= 0)
                throw new ArgumentException("numQuestions must be >= 1");

            var needed = numQuestions;

            var selectedQuestions = new List<QuestionDto>();
            var selectedIds = new List<long>();
            var consecutiveCorrect = new Dictionary<long, int>();

            // 1) UQS
            var uqs = await _repo.GetDueUserQuestionStatesAsync(userId, numQuestions, ct);
            var uqsOrderedIds = uqs.Select(x => x.QuestionId).ToList();
            foreach (var (qId, cc) in uqs)
                consecutiveCorrect[qId] = cc;

            if (uqsOrderedIds.Count > 0)
            {
                var qDtos = await _repo.GetQuestionsByIdsAsync(uqsOrderedIds, ct);
                // qDtos are returned in the same order as uqsOrderedIds
                foreach (var dto in qDtos)
                {
                    selectedQuestions.Add(dto);
                    selectedIds.Add(dto.Id);
                }
            }

            needed = numQuestions - selectedIds.Count;

            // 2) Fallback from recent seen events (7 days)
            if (needed > 0)
            {
                var fallback = await _repo.GetFallbackQuestionsFromRecentSeenAsync(userId, selectedIds, needed, ct);
                var addedIds = new List<long>();
                foreach (var dto in fallback)
                {
                    selectedQuestions.Add(dto);
                    addedIds.Add(dto.Id);
                }

                foreach (var id in addedIds)
                {
                    if (!consecutiveCorrect.ContainsKey(id))
                        consecutiveCorrect[id] = 0;
                    selectedIds.Add(id);
                }

                needed = numQuestions - selectedIds.Count;
            }

            if (selectedIds.Count == 0)
            {
                return new { error = "No due questions and no available recently seen questions in last 7 days." };
            }

            // 3) fetch choices
            var choicesByQuestion = await _repo.GetChoicesByQuestionIdsAsync(selectedIds, ct);

            // attach choices preserving selectedQuestions order
            foreach (var q in selectedQuestions)
            {
                if (choicesByQuestion.TryGetValue(q.Id, out var list))
                    q.Choices = list;
                else
                    q.Choices = new List<ChoiceDto>();
            }

            // Trim final list to requested count
            var finalList = selectedQuestions.Take(numQuestions).ToList();

            // Build payload akin to original
            var payload = new
            {
                userId = userId,
                count = finalList.Count,
                questions = finalList.Select(q => new
                {
                    questionId = q.Id,
                    objectId = q.ObjectId,
                    title = q.Title,
                    body = q.Body,
                    explanation = q.Explanation,
                    difficulty = q.Difficulty,
                    sponsor = q.Sponsor,
                    sponsorBody = q.SponsorBody,
                    sponsorLink = q.SponsorLink,
                    consecutiveCorrect = consecutiveCorrect.TryGetValue(q.Id, out var cc) ? cc : 0,
                    choices = q.Choices.Select(c => new
                    {
                        choiceId = c.Id,
                        content = c.Content,
                        isCorrect = c.IsCorrect
                    })
                })
            };

            return payload;
        }

        public async Task<object> GetRandomQuestionAsync(CancellationToken ct)
        {
            var q = await _repo.GetRandomActiveQuestionWithChoicesAsync(ct);
            if (q == null)
                return new { error = "No active questions found." };

            var payload = new
            {
                questionId = q.Id,
                objectId = q.ObjectId,
                title = q.Title,
                body = q.Body,
                explanation = q.Explanation,
                difficulty = q.Difficulty,
                sponsor = q.Sponsor,
                sponsorBody = q.SponsorBody,
                sponsorLink = q.SponsorLink,
                choices = q.Choices.Select(c => new
                {
                    choiceId = c.Id,
                    content = c.Content,
                    isCorrect = c.IsCorrect
                })
            };

            return payload;
        }

        public async Task<int> UpsertUserQuestionAsync(UserQuestionState request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return 0;
            var nextDueAt = DateSpacing.CalculateNextDueAt(request.ConsecutiveCorrect);

            return await _repo.UpsertUserQuestionAsync(
                request.UserId!.Trim(),
                request.QuestionId,
                nextDueAt,
                request.ConsecutiveCorrect,
                ct);
        }
    }
}
