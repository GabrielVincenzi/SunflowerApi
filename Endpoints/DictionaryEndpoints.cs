using Microsoft.AspNetCore.Mvc;
using SunflowerApi.Repositories;

public static class DictionaryEndpoints
{
    public static void RegisterDictionaryEndpoints(this WebApplication app)
    {
        var dict = app.MapGroup("/chart");

        dict.MapGet("/variableLabels", GetVariableLabels)
            .CacheOutput(builder => builder.Expire(TimeSpan.FromHours(6))
                .SetVaryByQuery("table", "lang", "vars"));

        static async Task<IResult> GetVariableLabels(
            [FromQuery] string table,
            [FromQuery] string vars,
            [FromQuery] string lang,
            [FromServices] IDictionaryRepository dictionaryRepository,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(table))
                return Results.BadRequest(new { error = "table is required." });
            if (string.IsNullOrWhiteSpace(vars))
                return Results.Ok(new Dictionary<string, string>());

            var codes = vars.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var effectiveLang = string.IsNullOrWhiteSpace(lang) ? "en" : lang;

            try
            {
                var labels = await dictionaryRepository.GetVariableLabelsAsync(table, effectiveLang, codes, ct);

                // fall back to code itself for any code with no stored label,
                // matching the old inline-resolution behavior
                var result = codes.ToDictionary(
                    code => code,
                    code => labels.TryGetValue(code, out var lbl) ? lbl : code);

                return Results.Ok(result);
            }
            catch (Exception)
            {
                return Results.Problem("Internal server error");
            }
        }
    }
}
