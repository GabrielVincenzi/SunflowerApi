using Microsoft.AspNetCore.Mvc;
using SunflowerApi.Services;

namespace SunflowerApi.Endpoints;

public static class SearchEndpoints
{
    public static void RegisterSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/chart/searchChart", HandleSearchChart)
           .WithName("SearchCharts")
           .WithTags("Chart")
           .Produces<ChartSearchResponse>(StatusCodes.Status200OK)
           .ProducesValidationProblem()
           .ProducesProblem(StatusCodes.Status500InternalServerError);
        ;
    }

    private static async Task<IResult> HandleSearchChart(
        [FromQuery] string? category,
        [FromQuery] string? source,
        [FromQuery] string? lang,
        [FromQuery] int? limit,
        [FromQuery(Name = "afterId")] long? afterCursor,
        [FromBody] SearchChartRequestBody? body,
        ISearchService searchService,
        CancellationToken ct)
    {
        try
        {
            var result = await searchService.SearchChartsAsync(
                new ChartSearchQuery(
                    Search: body?.Query,
                    Vector: body?.Vector,
                    Category: category,
                    Source: source,
                    Lang: lang,
                    Limit: limit,
                    AfterCursor: afterCursor),
                ct);

            return Results.Ok(new ChartSearchResponse(
                Data: result.Data,
                NextCursor: result.NextCursor,
                HasMore: result.HasMore,
                Limit: result.Limit));
        }
        catch (ArgumentException ex)
        {
            // Bad input (e.g. wrong embedding dimension) — 400, not 500.
            // Unhandled exceptions fall through to your global exception
            // handler / middleware and surface as 500s, same as elsewhere in the API.
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}

public sealed record SearchChartRequestBody(string? Query, float[]? Vector);

public sealed record ChartSearchResponse(
    List<Dictionary<string, object?>> Data,
    long? NextCursor,
    bool HasMore,
    int Limit);