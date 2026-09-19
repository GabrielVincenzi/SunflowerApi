using SunflowerApi.Services;
using SunflowerApi.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SunflowerApi.Endpoints
{
    public static class DataEndpoints
    {
        public static void RegisterDataEndpoints(this WebApplication app)
        {
            var charts = app.MapGroup("/chart");
            charts.MapGet("/getData", GetChartData).CacheOutput(builder => builder.Expire(TimeSpan.FromMinutes(5))
                                       .SetVaryByQuery("database", "geos", "variables", "startPeriod", "endPeriod"));
            charts.MapGet("/allCharts", GetSelectedCharts).CacheOutput(builder => builder.Expire(TimeSpan.FromMinutes(10))
                                       .SetVaryByQuery("category", "search", "lang", "afterId", "limit"));
            charts.MapGet("/recommended", GetRecommendedCharts).CacheOutput(builder => builder.Expire(TimeSpan.FromMinutes(15))
                                       .SetVaryByQuery("lang", "excludeSeenDays", "lastSimilarity", "afterId"));
            charts.MapGet("/random", GetRandomCharts).CacheOutput(builder => builder.Expire(TimeSpan.FromMinutes(15))
                                       .SetVaryByQuery("seed", "lang", "categories", "lastSortKey", "afterId"));

            // ----------------------------
            // Chart data
            // ----------------------------
            static async Task<IResult> GetChartData(
                [FromQuery] string database,
                [FromQuery] string? geos,
                [FromQuery] string? variables,
                [FromQuery] DateTime? startPeriod,
                [FromQuery] DateTime? endPeriod,
                [FromServices] IChartService chartService,
                CancellationToken ct)
            {
                if (string.IsNullOrWhiteSpace(database))
                    return Results.BadRequest(new { error = "Database/table name is required." });

                var geoArr = string.IsNullOrWhiteSpace(geos) ? Array.Empty<string>() : geos.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var varArr = string.IsNullOrWhiteSpace(variables) ? Array.Empty<string>() : variables.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                try
                {
                    var result = await chartService.GetChartDataAsync(database, geoArr, varArr, startPeriod, endPeriod, ct);
                    return Results.Ok(result);
                }
                catch (ArgumentException aex)
                {
                    return Results.BadRequest(new { error = aex.Message });
                }
                catch (OperationCanceledException)
                {
                    return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
                }
                catch (Exception)
                {
                    return Results.Problem("Internal server error");
                }
            }

            // ----------------------------
            // Selected charts
            // ----------------------------
            static async Task<IResult> GetSelectedCharts(
                [FromServices] IChartRepository chartRepository,
                [FromQuery] string? category,
                [FromQuery] string? search,
                [FromQuery] int limit,
                [FromQuery] long? afterId,
                [FromQuery] string lang,
                CancellationToken ct)
            {
                try
                {
                    var take = Math.Clamp(limit, 1, 100);
                    var (rows, nextCursor, hasMore) =
                        await chartRepository.GetSelectedChartsAsync(
                            category,
                            search,
                            take,
                            afterId,
                            lang,
                            ct);

                    return Results.Ok(new
                    {
                        data = rows,
                        limit = take,
                        nextCursor,
                        hasMore
                    });
                }
                catch (Exception)
                {
                    return Results.Problem("Internal server error");
                }
            }

            // ----------------------------
            // Recommended charts
            // ----------------------------
            static async Task<IResult> GetRecommendedCharts(
                HttpContext httpContext,
                [FromServices] IChartRepository chartRepository,
                [FromQuery] int limit,
                [FromQuery] int excludeSeenDays,
                [FromQuery] double? lastSimilarity,
                [FromQuery] long? afterId,
                [FromQuery] string lang,
                CancellationToken ct)
            {
                var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
              ?? httpContext.User.FindFirstValue("sub");
                if (string.IsNullOrWhiteSpace(userId))
                    return Results.Unauthorized();

                try
                {
                    var take = Math.Clamp(limit, 1, 100);

                    var (rows, nextCursor, hasMore) =
                        await chartRepository.GetRecommendedChartsAsync(
                            userId,
                            limit,
                            excludeSeenDays,
                            lang,
                            lastSimilarity,
                            afterId,
                            ct);

                    return Results.Ok(new
                    {
                        data = rows,
                        limit = take,
                        nextCursor,
                        hasMore
                    });
                }
                catch (Exception)
                {
                    return Results.Problem("Internal server error");
                }
            }

            // ----------------------------
            // Random charts
            // ----------------------------
            static async Task<IResult> GetRandomCharts(
                [FromServices] IChartRepository chartRepository,
                [FromQuery] string? seed,
                [FromQuery] int limit,
                [FromQuery] int categories,
                [FromQuery] string? lastSortKey,
                [FromQuery] long? afterId,
                [FromQuery] string lang,
                CancellationToken ct)
            {
                try
                {
                    var take = Math.Clamp(limit, 1, 100);
                    var catCount = Math.Max(1, categories);

                    var (rows, nextCursor, hasMore) =
                        await chartRepository.GetRandomChartsAsync(
                            take,
                            categories,
                            lang,
                            seed,
                            lastSortKey,
                            afterId,
                            ct);

                    return Results.Ok(new
                    {
                        data = rows,
                        limit = take,
                        nextCursor,
                        hasMore
                    });
                }
                catch (Exception)
                {
                    return Results.Problem("Internal server error");
                }
            }
        }
    }
}