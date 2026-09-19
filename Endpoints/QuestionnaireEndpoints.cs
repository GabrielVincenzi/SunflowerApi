using Microsoft.AspNetCore.Mvc;
using SunflowerApi.Services;
using SunflowerApi.Models;
using System.Security.Claims;

namespace QuestionnaireApi.Endpoints
{
    public static class QuestionnaireEndpoints
    {
        public static void RegisterQuestionEndpoints(this WebApplication app)
        {
            var questionnaire = app.MapGroup("/questionnaire");
            questionnaire.MapGet("/dailyquestion", GetQuestionOfTheDay).CacheOutput(builder => builder.Expire(TimeSpan.FromMinutes(30))
                                       .SetVaryByQuery("userId", "numQuestions"));
            questionnaire.MapGet("/random", GetRandomQuestion);
            questionnaire.MapPost("/userquestionstate", UpdateUserQuestionState);
        }

        static async Task<IResult> GetQuestionOfTheDay(
            HttpContext httpContext,
            [FromQuery] int numQuestions,
            [FromServices] IQuestionnaireService service,
            CancellationToken ct)
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
              ?? httpContext.User.FindFirstValue("sub");
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            if (numQuestions <= 0)
                return Results.BadRequest("numQuestions must be >= 1");

            try
            {
                var result = await service.GetQuestionOfTheDayAsync(userId, numQuestions, ct);
                // service returns error object in case of no data
                if (result is not null && result.GetType().GetProperty("error") != null)
                    return Results.NotFound(result);

                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        }

        static async Task<IResult> GetRandomQuestion(
            [FromServices] IQuestionnaireService service,
            CancellationToken ct)
        {
            var result = await service.GetRandomQuestionAsync(ct);
            if (result is not null && result.GetType().GetProperty("error") != null)
                return Results.NotFound(result);

            return Results.Ok(result);
        }

        static async Task<IResult> UpdateUserQuestionState(
            HttpContext httpContext,
            [FromBody] UserQuestionState req,
            [FromServices] IQuestionnaireService service,
            CancellationToken ct)
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
              ?? httpContext.User.FindFirstValue("sub");
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            var rows = await service.UpsertUserQuestionAsync(req, ct);

            return Results.Ok(new { affected = rows });
        }
    }
}
