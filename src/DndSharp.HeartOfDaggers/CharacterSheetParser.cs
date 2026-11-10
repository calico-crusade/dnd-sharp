using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using HtmlAgilityPack;

namespace DndSharp.HeartOfDaggers;

/// <summary>Builds the character summary used by the bot from the site's current sheet markup.</summary>
internal static class CharacterSheetParser
{
    public static JsonElement Parse(string html, JsonElement state, string? campaignName)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var nodes = document.DocumentNode.SelectNodes("//*[@data-dhc-json]") ?? Enumerable.Empty<HtmlNode>();
        var weapons = new JsonArray();
        var armor = new JsonArray();
        string? ancestry = null;
        foreach (var node in nodes)
        {
            try
            {
                using var json = JsonDocument.Parse(WebUtility.HtmlDecode(node.GetAttributeValue("data-dhc-json", "")));
                var data = json.RootElement;
                if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String)
                    continue;
                var type = kind.GetString();
                if (type == "ancestry" && node.Name == "button" && data.TryGetProperty("name", out var name))
                    ancestry ??= name.GetString();
                if (type is "weapon" or "armor")
                {
                    var clean = JsonNode.Parse(EmbeddedState.RemoveCredentials(data).GetRawText());
                    if (type == "weapon") weapons.Add(clean);
                    else armor.Add(clean);
                }
            }
            catch (JsonException)
            {
                // Ignore malformed embedded item data.
            }
        }

        JsonNode? Value(string key) => state.TryGetProperty(key, out var value) ? JsonNode.Parse(value.GetRawText()) : null;
        string? Text(HtmlNode? node) => node is null ? null : WebUtility.HtmlDecode(node.InnerText).Trim();
        string? Label(string label) => document.DocumentNode.SelectSingleNode($"//*[@data-dhc-label='{label}']")?.GetAttributeValue("data-dhc-base", null);
        string? Effective(string field) => document.DocumentNode.SelectSingleNode($"//*[@data-dhc-effective-field='{field}'][@data-dhc-base]")?.GetAttributeValue("data-dhc-base", null);

        var traits = new JsonArray();
        foreach (var key in new[] { "agility", "strength", "finesse", "instinct", "presence", "knowledge" })
            traits.Add(new JsonObject { ["key"] = key, ["value"] = Value(key + "Mod") });
        var trackers = new JsonObject();
        foreach (var key in new[] { "hp", "stress", "hope", "armor_score" })
        {
            var tracker = document.DocumentNode.SelectSingleNode($"//*[@data-field='{key}'][@data-value][@data-max]");
            if (tracker is not null)
                trackers[key] = new JsonObject { ["value"] = tracker.GetAttributeValue("data-value", ""), ["max"] = tracker.GetAttributeValue("data-max", "") };
        }
        var experiences = new JsonArray();
        if (state.TryGetProperty("experiences", out var experienceData) && experienceData.ValueKind == JsonValueKind.Array)
            foreach (var item in experienceData.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object)
                    experiences.Add(new JsonObject
                    {
                        ["name"] = item.TryGetProperty("name", out var name) ? name.ToString() : "",
                        ["modifier"] = item.TryGetProperty("value", out var modifier) ? modifier.ToString() : ""
                    });

        JsonNode? domainCards = null;
        var domain = Text(document.GetElementbyId("dh-visual-domain-cards-data"));
        if (!string.IsNullOrWhiteSpace(domain))
        {
            try
            {
                using var json = JsonDocument.Parse(domain);
                domainCards = JsonNode.Parse(EmbeddedState.RemoveCredentials(json.RootElement).GetRawText());
            }
            catch (JsonException) { }
        }

        var character = new JsonObject
        {
            ["id"] = Value("postId"),
            ["name"] = campaignName ?? Value("postTitle")?.ToString(),
            ["level"] = Value("level"),
            ["portraitUrl"] = Value("featuredImage"),
            ["className"] = Value("className"),
            ["subclassName"] = Value("subclassName"),
            ["ancestry"] = ancestry,
            ["community"] = Value("communityName"),
            ["traits"] = traits,
            ["evasion"] = Label("Evasion"),
            ["stats"] = new JsonObject { ["armorScore"] = Label("Armor"), ["proficiency"] = Label("Proficiency") },
            ["thresholds"] = new JsonObject
            {
                ["major"] = Effective("modifiers.computed.threshold_major"),
                ["severe"] = Effective("modifiers.computed.threshold_severe")
            },
            ["trackers"] = trackers,
            ["experiences"] = experiences,
            ["equipment"] = new JsonObject { ["weapons"] = weapons, ["armor"] = armor },
            ["domainCards"] = domainCards
        };
        return JsonSerializer.SerializeToElement(character);
    }
}
