using Microsoft.AspNetCore.Mvc;
using SunflowerApi.Services;

public static class DbEndpoints
{
    public static void RegisterDbMetadataEndpoints(this WebApplication app)
    {
        var dbItems = app.MapGroup("/db");

        dbItems.MapGet("/", GetDbMetadata).CacheOutput(p => p.Expire(TimeSpan.FromHours(1))); ;
        dbItems.MapGet("/categories", GetCategories).CacheOutput(p => p.Expire(TimeSpan.FromHours(1)));
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
}
