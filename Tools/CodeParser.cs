public static class VarCodesParser
{
    public static IEnumerable<string> ParseVarCodes(object? varsValue)
    {
        if (varsValue is null) return Enumerable.Empty<string>();

        var raw = varsValue switch
        {
            string s => s,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } je
                => je.GetRawText(),
            _ => varsValue.ToString() ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(raw)) return Enumerable.Empty<string>();

        // Try JSON array first: ["code1", "code2"]
        if (raw.TrimStart().StartsWith("["))
        {
            try
            {
                return System.Text.Json.JsonSerializer
                    .Deserialize<string[]>(raw) ?? Array.Empty<string>();
            }
            catch { /* fall through */ }
        }

        // Plain delimited string — try + first, then comma
        var separator = raw.Contains('+') ? '+' : ',';
        return raw.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}