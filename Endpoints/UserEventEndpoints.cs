using SunflowerApi.Repositories;
using SunflowerApi.Services;
using SunflowerApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace SunflowerApi.Endpoints
{
    public static class EventEndpoints
    {
        public static void RegisterUserEventEndpoints(this WebApplication app)
        {
            var events = app.MapGroup("/events");
            events.MapGet("/savedIds", GetSavedEventIds);
            events.MapGet("/saved", GetSavedEvents);
            events.MapGet("/", GetEventsByFilter);
            events.MapPost("/new", CreateEvent);
            events.MapDelete("/saved/del", DeleteSaved);

            static async Task<IResult> GetSavedEventIds(
                HttpContext httpContext,
                [FromServices] IUserEventService userEventService,
                CancellationToken ct)
            {
                var userId = httpContext.User.GetUserId();
                if (userId is null) return Results.Unauthorized();

                var result = await userEventService.GetSavedEventIdsAsync(userId, ct);
                return Results.Ok(result);
            }

            static async Task<IResult> GetSavedEvents(
                HttpContext httpContext,
                [FromQuery] string? lang,
                [FromServices] IUserEventService userEventService,
                CancellationToken ct)
            {
                var userId = httpContext.User.GetUserId();
                if (userId is null) return Results.Unauthorized();

                var result = await userEventService.GetSavedEventsAsync(userId, lang ?? "en", ct);
                return Results.Ok(result);
            }

            static async Task<IResult> GetEventsByFilter(
                HttpContext httpContext,
                [FromQuery] string action,
                [FromQuery] Guid? objectId,
                [FromQuery] DateTime? from,
                [FromQuery] DateTime? to,
                [FromServices] IUserEventService userEventService,
                CancellationToken ct)
            {
                if (string.IsNullOrWhiteSpace(action))
                    return Results.BadRequest(new { error = "The 'action' query parameter is required." });

                if (from.HasValue && to.HasValue && from > to)
                    return Results.BadRequest(new { error = "'from' must be earlier than 'to'." });

                var userId = httpContext.User.GetUserId();
                if (userId is null) return Results.Unauthorized();

                var events = await userEventService.GetFilteredEventsAsync(userId, action, objectId, from, to, ct);
                return Results.Ok(new { data = events });
            }

            static async Task<IResult> CreateEvent(
                [FromBody] UserEvent req,
                [FromServices] IUserEventService userEventService,
                HttpContext httpContext,
                CancellationToken ct)
            {
                if (req == null)
                    return Results.BadRequest();

                var userId = httpContext.User.GetUserId();
                if (userId is null) return Results.Unauthorized();

                var rows = await userEventService.CreateEventAsync(
                    userId,
                    req.Action!,
                    req.ObjectId!,
                    ct);

                return Results.Created("/events/new", new { inserted = rows });
            }

            static async Task<IResult> DeleteSaved(
                HttpContext httpContext,
                [FromQuery] Guid objectId,
                [FromServices] IUserEventService userEventService,
                CancellationToken ct)
            {
                var userId = httpContext.User.GetUserId();
                if (userId is null) return Results.Unauthorized();

                var rows = await userEventService.DeleteSavedAsync(userId, objectId, ct);

                if (rows == 0)
                    return Results.NotFound(new { error = "No matching record", userId, objectId });

                return Results.Ok(new { deleted = rows });
            }
        }
    }
}