using System.Text.Json;

// Works on one chart's `vars` column (jsonb):
//
//   { "<root>": { "pattern": [codelistOfRoot, codelistDepth1, codelistDepth2, ...],
//                 "values":  { "<code1>": { "<code2>": ["<code3>", ...] } } } }
//
// pattern[0]  -> codelist of the root code itself
// pattern[d]  -> codelist of the keys / leaf items found at depth d of "values" (d >= 1)
//
// Output sent to the UI:
//   vars[root]   = the "values" tree, codes only (pattern is dropped).
//                  null  -> no attributes: the root code itself is the variable
//                  "t"   -> single leaf code: variable is root_t
//   labels[root] = [rootLabel, labelNode]   where labelNode mirrors the values tree:
//        object node -> { code: [codeLabel, labelNodeOfChild] }   (same keys as vars)
//        array node  -> [label0, label1, ...]                      (same order as vars)
//
// Everything is addressed by code key or array index, never by label, so repeated
// codes in different dimensions can't collide and no ordering assumption is needed.
public static class CodeParser
{
    // A real JSON null (default(JsonElement) can't be serialized).
    private static readonly JsonElement NullElement = JsonDocument.Parse("null").RootElement;

    public sealed record RootVars(string Root, string[] Pattern, JsonElement Values);

    public static List<RootVars> ParseVars(JsonElement vars)
    {
        var list = new List<RootVars>();
        if (vars.ValueKind != JsonValueKind.Object) return list;

        foreach (var root in vars.EnumerateObject())
        {
            if (root.Value.ValueKind != JsonValueKind.Object) continue;

            // "values" may be null (or missing) BY DESIGN: the variable has no varying
            // attributes, so the root code alone is the selectable variable. Keep it and
            // send it to the UI as null instead of dropping the root.
            var values = root.Value.TryGetProperty("values", out var v) ? v : NullElement;

            var pattern = root.Value.TryGetProperty("pattern", out var p) && p.ValueKind == JsonValueKind.Array
                ? p.EnumerateArray().Select(e => e.GetString() ?? "").ToArray()
                : Array.Empty<string>();

            list.Add(new RootVars(root.Name, pattern, values));
        }
        return list;
    }

    // Pass 1: gather every (codelist, code) that needs a label, so the caller
    // can resolve them all with ONE dictionary query.
    public static void CollectCodes(RootVars r, HashSet<(string codelist, string code)> needed)
    {
        if (r.Pattern.Length > 0) needed.Add((r.Pattern[0], r.Root));
        Collect(r.Values, r.Pattern, 1, needed);
    }

    private static void Collect(JsonElement node, string[] pattern, int depth,
        HashSet<(string, string)> needed)
    {
        if (depth >= pattern.Length) return;
        var codelist = pattern[depth];

        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in node.EnumerateObject())
                {
                    needed.Add((codelist, prop.Name));
                    Collect(prop.Value, pattern, depth + 1, needed);
                }
                break;
            case JsonValueKind.Array:
                foreach (var e in node.EnumerateArray())
                    if (e.ValueKind == JsonValueKind.String) needed.Add((codelist, e.GetString()!));
                break;
            case JsonValueKind.String:
                needed.Add((codelist, node.GetString()!));
                break;
        }
    }

    // Pass 2: build labels[root] = [rootLabel, labelTree]
    public static object?[] BuildLabels(RootVars r, Dictionary<(string, string), string> resolved)
    {
        var rootCodelist = r.Pattern.Length > 0 ? r.Pattern[0] : null;
        return new object?[]
        {
            Lookup(rootCodelist, r.Root, resolved),
            BuildNode(r.Values, r.Pattern, 1, resolved)
        };
    }

    private static object? BuildNode(JsonElement node, string[] pattern, int depth,
        Dictionary<(string, string), string> resolved)
    {
        var codelist = depth < pattern.Length ? pattern[depth] : null;

        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new Dictionary<string, object?>();
                foreach (var prop in node.EnumerateObject())
                {
                    obj[prop.Name] = new object?[]
                    {
                        Lookup(codelist, prop.Name, resolved),
                        BuildNode(prop.Value, pattern, depth + 1, resolved)
                    };
                }
                return obj;

            case JsonValueKind.Array:
                return node.EnumerateArray()
                    .Select(e => (object?)Lookup(codelist, e.GetString() ?? "", resolved))
                    .ToList();

            case JsonValueKind.String:
                return Lookup(codelist, node.GetString()!, resolved);

            default:
                return null;
        }
    }

    // Falls back to the raw code when no label exists (same behaviour as before).
    private static string Lookup(string? codelist, string code,
        Dictionary<(string, string), string> resolved) =>
        codelist != null && resolved.TryGetValue((codelist, code), out var label) ? label : code;
}