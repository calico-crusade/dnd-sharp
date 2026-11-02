using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DndSharp.HeartOfDaggers;

internal static class EmbeddedState
{
    public static JsonElement? Extract(string html, string name)
    {
        var assignments = Regex.Matches(html, @"window\." + Regex.Escape(name) + @"\s*=\s*(?=\{)", RegexOptions.CultureInvariant);
        foreach (Match assignment in assignments)
        {
            var jsonBytes = Encoding.UTF8.GetBytes(html[(assignment.Index + assignment.Length)..]);
            var reader = new Utf8JsonReader(jsonBytes, isFinalBlock: true, state: default);
            try
            {
                using var document = JsonDocument.ParseValue(ref reader);
                if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.EnumerateObject().Any())
                    return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                // Ignore script assignments that are JavaScript expressions rather than embedded JSON.
            }
        }
        return null;
    }

    public static JsonElement RemoveCredentials(JsonElement value)
    {
        var node = JsonNode.Parse(value.GetRawText())!;
        Scrub(node);
        return JsonSerializer.SerializeToElement(node);
    }

    private static void Scrub(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(property => property.Key).ToArray())
            {
                if (IsCredential(key)) obj.Remove(key);
                else if (obj[key] is { } child) Scrub(child);
            }
        }
        else if (node is JsonArray array)
            foreach (var child in array) if (child is not null) Scrub(child);
    }

    private static bool IsCredential(string key) =>
        key.EndsWith("nonce", StringComparison.OrdinalIgnoreCase) ||
        key.EndsWith("token", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("cookie", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("authorization", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("password", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("secret", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("apiKey", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("sessionToken", StringComparison.OrdinalIgnoreCase);
}
