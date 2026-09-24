using Microsoft.AspNetCore.Mvc;
using SunflowerApi.Services;

public static class DbEndpoints
{
    public static void RegisterDbMetadataEndpoints(this WebApplication app)
    {
        var dbItems = app.MapGroup("/db");

        dbItems.MapGet("/", GetDbMetadata)
        .CacheShared(TimeSpan.FromHours(6), CacheTags.DbFilters);

        dbItems.MapGet("/categories", GetCategories)
        .CacheShared(TimeSpan.FromHours(6), CacheTags.DbFilters, "lang");

        dbItems.MapGet("/sources", GetSources)
        .CacheShared(TimeSpan.FromHours(6), CacheTags.DbFilters, "lang");
    }

    private static async Task<IResult> GetDbMetadata(
        [FromQuery] string name,
        [FromServices] IDbMetadataService service,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Results.BadRequest("name is required");

        var db = await service.GetDbMetadataAsync(name, ct);
        if (db is null)
            return Results.NotFound();

        return Results.Ok(db);
    }

    private static async Task<IResult> GetCategories(
        [FromQuery] string lang,
        [FromServices] IDbMetadataService service,
        CancellationToken ct)
    {
        var categories = await service.GetCategoriesAsync(lang, ct);
        return Results.Ok(categories);
    }

    private static async Task<IResult> GetSources(
        [FromQuery] string lang,
        [FromServices] IDbMetadataService service,
        CancellationToken ct)
    {
        var sources = await service.GetSourcesAsync(lang, ct);
        return Results.Ok(sources);
    }
}
