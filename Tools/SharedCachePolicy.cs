using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

public sealed class SharedCachePolicy : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken ct)
    {
        var method = context.HttpContext.Request.Method;
        var cacheable = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        context.AllowLocking = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken ct)
        => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken ct)
    {
        var response = context.HttpContext.Response;

        // Only cache successful responses that don't set cookies
        if (response.StatusCode != StatusCodes.Status200OK ||
            !StringValues.IsNullOrEmpty(response.Headers.SetCookie))
        {
            context.AllowCacheStorage = false;
        }
        return ValueTask.CompletedTask;
    }
}

public static class CacheExtensions
{
    public static RouteHandlerBuilder CacheShared(
        this RouteHandlerBuilder builder, TimeSpan ttl, string tag, params string[] varyByQuery)
        => builder.CacheOutput(p => p
            .Expire(ttl)
            .SetVaryByQuery(varyByQuery)
            .Tag(tag),
            excludeDefaultPolicy: true);
}

public static class CacheTags
{
    public const string ChartData = "chart-data";
    public const string ChartLists = "chart-lists";
    public const string ChartRandom = "chart-random";
    public const string DbFilters = "db-filters";

    public static readonly HashSet<string> All =
        new() { ChartData, ChartLists, ChartRandom, DbFilters };
}