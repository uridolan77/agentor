using System.Text.Json.Nodes;

namespace Agentor.Application.Redaction;

public static class JsonRedaction
{
    public static RedactionResult Apply(JsonNode? node, RedactionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var paths = new List<string>();
        var count = ApplyCore(node, policy, "", paths);
        return new RedactionResult(count, paths);
    }

    private static int ApplyCore(JsonNode? node, RedactionPolicy policy, string pathPrefix, List<string> paths)
    {
        if (node is null || policy.KeyNameSubstrings.Count == 0)
        {
            return 0;
        }

        var count = 0;
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                var segment = "/" + EscapeJsonPointerSegment(property.Key);
                var path = pathPrefix + segment;
                if (ShouldRedactKey(property.Key, policy.KeyNameSubstrings))
                {
                    obj[property.Key] = JsonValue.Create(policy.ReplacementToken);
                    count++;
                    paths.Add(path);
                }
                else
                {
                    count += ApplyCore(property.Value, policy, path, paths);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                count += ApplyCore(arr[i], policy, pathPrefix + "/" + i, paths);
            }
        }

        return count;
    }

    private static bool ShouldRedactKey(string key, IReadOnlyList<string> sensitiveSubstrings)
    {
        foreach (var s in sensitiveSubstrings)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                continue;
            }

            if (key.Contains(s, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string EscapeJsonPointerSegment(string key)
    {
        return key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }
}