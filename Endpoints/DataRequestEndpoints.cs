using Microsoft.AspNetCore.Mvc;
using SunflowerApi.Models;
using SunflowerApi.Services;
using System.Security.Claims;

namespace SunflowerApi.Endpoints
{
    public static class DataRequestEndpoints
    {
        public static void RegisterDataRequestEndpoints(this WebApplication app)
        {
            var requests = app.MapGroup("/requests");

            requests.MapPost("/new", SubmitRequest)
                    .RequireRateLimiting("data-request");
        }

        static async Task<IResult> SubmitRequest(
            [FromBody] DataRequest req,
            [FromServices] IDataRequestService dataRequestService,
            HttpContext httpContext,
            CancellationToken ct)
        {
            // ── Input validation ────────────────────────────────────────────
            if (req is null)
                return Results.BadRequest(new { error = "Request body is required." });

            if (string.IsNullOrWhiteSpace(req.Message))
                return Results.BadRequest(new { error = "message is required." });

            if (req.Message.Trim().Length < 10)
                return Results.BadRequest(new { error = "Message must be at least 10 characters." });

            if (req.Message.Length > 2000)
                return Results.BadRequest(new { error = "Message cannot exceed 2000 characters." });

            var userId = httpContext.User.GetUserId();
            if (userId is null) return Results.Unauthorized();

            req.UserId = userId;

            // ── Delegate to service ─────────────────────────────────────────
            try
            {
                var requestId = await dataRequestService.SubmitRequestAsync(req, ct);

                if (requestId == Guid.Empty)
                    return Results.StatusCode(500);

                return Results.Created($"/requests/{requestId}", new { requestId });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }
    }
}