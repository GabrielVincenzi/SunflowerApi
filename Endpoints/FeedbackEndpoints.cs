using Microsoft.AspNetCore.Mvc;
using SunflowerApi.Services;
using System.Security.Claims;

namespace SunflowerApi.Endpoints
{
    public static class FeedbackEndpoints
    {
        public static void RegisterFeedbackEndpoints(this WebApplication app)
        {
            var feedback = app.MapGroup("/feedback");

            feedback.MapGet("/", GetFeedback).AllowAnonymous();
            feedback.MapPost("/", CreateFeedback).AllowAnonymous();
            feedback.MapPost("/{id:guid}/vote", VoteFeedback).AllowAnonymous();

            static async Task<IResult> GetFeedback(
                HttpContext httpContext,
                [FromServices] IFeedbackService feedbackService,
                CancellationToken ct)
            {
                // FallbackPolicy already requires an authenticated user for every
                // endpoint, so this is always populated by the time we get here.
                var userId = httpContext.User.FindFirstValue("name");

                try
                {
                    var items = await feedbackService.GetAllAsync(userId, ct);
                    return Results.Ok(items);
                }
                catch (Exception)
                {
                    return Results.Problem("Internal server error");
                }
            }

            static async Task<IResult> CreateFeedback(
                HttpContext httpContext,
                [FromBody] CreateFeedbackRequest request,
                [FromServices] IFeedbackService feedbackService,
                CancellationToken ct)
            {
                var userId = httpContext.User.FindFirstValue("name");
                if (string.IsNullOrWhiteSpace(userId))
                    return Results.Unauthorized();

                if (string.IsNullOrWhiteSpace(request.Message))
                    return Results.BadRequest(new { error = "message is required." });

                if (request.Message.Length > 500)
                    return Results.BadRequest(new { error = "message must be 500 characters or fewer." });

                // Adjust to whatever claim your Clerk session token actually carries
                // the display name in (Clerk doesn't include one by default).
                var authorName = httpContext.User.FindFirst("name")?.Value;

                try
                {
                    var item = await feedbackService.CreateAsync(userId, authorName, request.Message, ct);
                    return Results.Ok(item);
                }
                catch (Exception)
                {
                    return Results.Problem("Internal server error");
                }
            }

            static async Task<IResult> VoteFeedback(
                HttpContext httpContext,
                Guid id,
                [FromBody] VoteFeedbackRequest request,
                [FromServices] IFeedbackService feedbackService,
                CancellationToken ct)
            {
                var userId = httpContext.User.FindFirstValue("name");
                if (string.IsNullOrWhiteSpace(userId))
                    return Results.Unauthorized();

                if (request.Direction != "up" && request.Direction != "down")
                    return Results.BadRequest(new { error = "direction must be 'up' or 'down'." });

                try
                {
                    var item = await feedbackService.VoteAsync(id, userId, request.Direction, ct);
                    if (item is null)
                        return Results.NotFound();

                    return Results.Ok(item);
                }
                catch (Exception)
                {
                    return Results.Problem("Internal server error");
                }
            }
        }

        public record CreateFeedbackRequest(string Message);
        public record VoteFeedbackRequest(string Direction);
    }
}