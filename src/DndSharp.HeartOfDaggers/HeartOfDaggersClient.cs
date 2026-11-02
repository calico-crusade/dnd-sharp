using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace DndSharp.HeartOfDaggers;

/// <summary>Read-only access to Heart of Daggers' JSON endpoints.</summary>
/// <remarks>
/// Responses are retained as JSON so new fields and site-specific character details are not discarded.
/// The HTTP client is responsible for the caller's authentication; no credentials are embedded here.
/// </remarks>
public sealed class HeartOfDaggersClient(HttpClient http)
{
    public Task<JsonElement> GetCampaignsAsync(CancellationToken token = default) =>
        GetJsonAsync("wp-json/hod-owlbear/v1/campaigns", token);

    public Task<JsonElement> GetCampaignCharactersAsync(long campaignId, CancellationToken token = default) =>
        GetJsonAsync($"wp-json/hod-owlbear/v1/campaigns/{PositiveId(campaignId)}/characters", token);

    public Task<JsonElement> GetCharacterAsync(long characterId, CancellationToken token = default) =>
        GetJsonAsync($"wp-json/hod-owlbear/v1/characters/{PositiveId(characterId)}", token);

    /// <summary>Load the campaign page and its connection information using the configured session.</summary>
    public async Task<CampaignSession> GetCampaignAsync(string campaign, CancellationToken token = default)
    {
        var slug = CampaignSession.ParseCampaignSlug(campaign);
        using var response = await http.GetAsync($"campaign/{slug}/", token);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.Redirect)
            throw new UnauthorizedAccessException("Heart of Daggers rejected the session or denied access to this campaign.");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(token);
        return CampaignSession.Parse(slug, html);
    }

    /// <summary>Read persisted campaign history; this read-only AJAX action uses POST.</summary>
    public async Task<IReadOnlyList<JsonElement>> GetCampaignHistoryAsync(CampaignSession campaign, CancellationToken token = default)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["action"] = "dh_campaign_dice_history_get",
            ["nonce"] = campaign.Nonce,
            ["campaign_id"] = campaign.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        using var response = await http.PostAsync("wp-admin/admin-ajax.php", form, token);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.Redirect)
            throw new UnauthorizedAccessException("Heart of Daggers rejected the session or denied access to campaign history.");
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        var root = document.RootElement;
        if (!root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("data", out var data) || !data.TryGetProperty("history", out var history) ||
            history.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Heart of Daggers did not return campaign history. The session may have expired.");
        return history.EnumerateArray().Select(item => item.Clone()).ToArray();
    }

    /// <summary>Fetch the full available Owlbear character data for every campaign character.</summary>
    public async Task<IReadOnlyList<CharacterSnapshot>> GetCampaignCharacterDetailsAsync(long campaignId, CancellationToken token = default)
    {
        var result = await GetCampaignCharactersAsync(campaignId, token);
        if (!result.TryGetProperty("characters", out var characters) || characters.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Heart of Daggers did not return a campaign character list.");
        var ids = new List<long>();
        foreach (var character in characters.EnumerateArray())
        {
            if (!character.TryGetProperty("id", out var id) || !id.TryGetInt64(out var characterId) || characterId <= 0)
                throw new InvalidOperationException("A campaign character is missing its numeric ID.");
            ids.Add(characterId);
        }
        return await GetCharacterDetailsAsync(ids, token);
    }

    /// <summary>Read character details using the IDs visible to this campaign member.</summary>
    public Task<IReadOnlyList<CharacterSnapshot>> GetCampaignCharacterDetailsAsync(CampaignSession campaign, CancellationToken token = default) =>
        GetCharacterDetailsAsync(campaign.CharacterIds, token);

    public async Task<CharacterSnapshot> GetCharacterDetailsAsync(long characterId, CancellationToken token = default) =>
        (await GetCharacterDetailsAsync([PositiveId(characterId)], token))[0];

    private async Task<IReadOnlyList<CharacterSnapshot>> GetCharacterDetailsAsync(IEnumerable<long> ids, CancellationToken token)
    {
        var details = new List<CharacterSnapshot>();
        foreach (var characterId in ids.Distinct())
        {
            var detail = await GetCharacterAsync(characterId, token);
            if (!detail.TryGetProperty("character", out var fullCharacter) || fullCharacter.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"Heart of Daggers did not return details for character {characterId}.");
            var sheet = await GetCharacterSheetStateAsync(fullCharacter, characterId, token);
            details.Add(new CharacterSnapshot(EmbeddedState.RemoveCredentials(fullCharacter), sheet.State, sheet.Features, sheet.Inventory));
        }
        return details;
    }

    private async Task<(JsonElement? State, IReadOnlyList<CharacterFeature> Features, CharacterInventory? Inventory)> GetCharacterSheetStateAsync(JsonElement character, long characterId, CancellationToken token)
    {
        if (!character.TryGetProperty("fullSheetUrl", out var value) || value.ValueKind != JsonValueKind.String ||
            !Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("heartofdaggers.com", StringComparison.OrdinalIgnoreCase)) return (null, [], null);
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !parts[0].Equals("character", StringComparison.OrdinalIgnoreCase) ||
            !parts[1].All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) return (null, [], null);

        using var response = await http.GetAsync($"character/{parts[1]}/", token);
        // Some campaign members may not share their full sheet with the caller.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Redirect)
            return (null, [], null);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(token);
        var state = EmbeddedState.Extract(html, "DH_CHARACTER_STATE");
        var inventory = await GetInventoryAsync(html, characterId, token);
        return (state is null ? null : EmbeddedState.RemoveCredentials(state.Value), ParseFeatures(html), inventory);
    }

    private async Task<CharacterInventory?> GetInventoryAsync(string html, long characterId, CancellationToken token)
    {
        // The site exposes the read-only inventory action in the sheet's JavaScript configuration.
        var config = Regex.Match(html, @"window\.DHC_INV\s*=\s*\{(?<body>[^}]{0,10000})\}\s*;", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!config.Success) return null;
        var body = config.Groups["body"].Value;
        var nonce = Regex.Match(body, "\\bnonce\\s*:\\s*['\"](?<value>[A-Za-z0-9]+)['\"]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var id = Regex.Match(body, @"\bpostId\s*:\s*(?<value>\d+)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!nonce.Success || !long.TryParse(id.Groups["value"].Value, out var postId) || postId != characterId) return null;
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["action"] = "dhc_inventory_get", ["nonce"] = nonce.Groups["value"].Value, ["post_id"] = characterId.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        try
        {
            using var response = await http.PostAsync("wp-admin/admin-ajax.php", form, token);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: token);
            return ParseInventory(json.RootElement);
        }
        catch (JsonException) { return null; }
        catch (HttpRequestException) { return null; }
    }

    internal static CharacterInventory? ParseInventory(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Object || !response.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True ||
            !response.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return null;
        var items = new List<CharacterInventoryItem>();
        var core = data.TryGetProperty("core_items", out var coreItems) && coreItems.ValueKind == JsonValueKind.Object ? coreItems : default;
        if (core.ValueKind == JsonValueKind.Undefined && data.TryGetProperty("starting_items", out var starting) && starting.ValueKind == JsonValueKind.Object && starting.TryGetProperty("core_items", out var startingCore) && startingCore.ValueKind == JsonValueKind.Object) core = startingCore;
        var entries = data.TryGetProperty("fixed2", out var fixed2) && fixed2.ValueKind == JsonValueKind.Array ? fixed2 :
            data.TryGetProperty("fixed", out var legacy) && legacy.ValueKind == JsonValueKind.Array ? legacy : default;
        if (entries.ValueKind == JsonValueKind.Array)
        foreach (var item in entries.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String) { var name = item.GetString(); if (!string.IsNullOrWhiteSpace(name)) items.Add(new(name, "", 1)); continue; }
            if (item.ValueKind != JsonValueKind.Object) continue;
            string? Value(JsonElement obj, string key) => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number ? v.ToString() : null;
            var nameValue = Value(item, "text") ?? Value(item, "name"); var description = "";
            var key = Value(item, "key");
            if (!string.IsNullOrEmpty(key) && core.ValueKind == JsonValueKind.Object && core.TryGetProperty(key, out var record) && record.ValueKind == JsonValueKind.Object)
            {
                nameValue = Value(record, "name_override");
                if (string.IsNullOrWhiteSpace(nameValue)) nameValue = Value(record, "name") ?? Value(record, "line");
                if (record.TryGetProperty("data", out var detail)) description = Value(detail, "description") ?? Value(detail, "feat_text") ?? "";
            }
            if (string.IsNullOrWhiteSpace(nameValue) && !string.IsNullOrWhiteSpace(key))
                nameValue = key.Split(':').Last().Replace('-', ' ').Replace('_', ' ');
            if (string.IsNullOrWhiteSpace(nameValue)) continue;
            var quantity = int.TryParse(Value(item, "q") ?? Value(item, "qty"), out var q) ? Math.Max(1, q) : 1;
            items.Add(new(nameValue, description, quantity));
        }
        int Gold(string key) => data.TryGetProperty(key, out var value) && int.TryParse(value.ToString(), out var amount) ? amount : 0;
        return new CharacterInventory(items, Gold("gold_coins"), Gold("gold_handfuls"), Gold("gold_bags"), Gold("gold_chests"));
    }

    internal static IReadOnlyList<CharacterFeature> ParseFeatures(string html)
    {
        var document = new HtmlDocument(); document.LoadHtml(html);
        var features = new List<CharacterFeature>();
        var panels = document.DocumentNode.SelectNodes("//*[contains(concat(' ', normalize-space(@class), ' '), ' dhc-feature-panel ') or @data-acc-title='Transformation']") ?? Enumerable.Empty<HtmlNode>();
        foreach (var panel in panels)
        {
            var category = WebUtility.HtmlDecode(panel.GetAttributeValue("data-acc-title", ""));
            if (category is not ("Class" or "Subclass" or "Ancestry & Community" or "Transformation")) continue;
            if (category == "Transformation")
            {
                foreach (var node in panel.SelectNodes(".//button[@data-dhc-json]") ?? Enumerable.Empty<HtmlNode>())
                {
                    try
                    {
                        using var json = JsonDocument.Parse(WebUtility.HtmlDecode(node.GetAttributeValue("data-dhc-json", "")));
                        var root = json.RootElement;
                        AddFeature(features, category, root, "name", "description");
                        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("feats", out var feats) && feats.ValueKind == JsonValueKind.Array)
                            foreach (var feat in feats.EnumerateArray()) AddFeature(features, category, feat, "name", "text");
                    }
                    catch (JsonException) { }
                }
            }
            if (category == "Class")
            {
                foreach (var node in panel.SelectNodes(".//button[@data-dhc-json]") ?? Enumerable.Empty<HtmlNode>())
                {
                    try
                    {
                        using var json = JsonDocument.Parse(WebUtility.HtmlDecode(node.GetAttributeValue("data-dhc-json", "")));
                        var root = json.RootElement;
                        if (root.ValueKind != JsonValueKind.Object) continue;
                        if (root.TryGetProperty("class_feats", out var classFeatures) && classFeatures.ValueKind == JsonValueKind.Array)
                            foreach (var feature in classFeatures.EnumerateArray()) AddFeature(features, "Class", feature, "name", "text");
                        AddFeature(features, "Hope Feature", root, "hope_feat_name", "hope_feat_text");
                    }
                    catch (JsonException) { /* Ignore malformed site feature data. */ }
                }
            }
            foreach (var node in panel.SelectNodes(".//li[@data-dhc-feature-actions][@data-dhc-json]") ?? Enumerable.Empty<HtmlNode>())
            {
                try
                {
                    using var json = JsonDocument.Parse(WebUtility.HtmlDecode(node.GetAttributeValue("data-dhc-json", "")));
                    AddFeature(features, category, json.RootElement, "name", "text");
                }
                catch (JsonException) { /* Ignore malformed site feature data. */ }
            }
        }
        return features;
    }

    private static void AddFeature(List<CharacterFeature> features, string category, JsonElement root, string nameKey, string textKey)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(nameKey, out var name) || name.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty(textKey, out var text) || text.ValueKind != JsonValueKind.String) return;
        var nameValue = name.GetString(); var textValue = text.GetString();
        if (!string.IsNullOrWhiteSpace(nameValue) && !string.IsNullOrWhiteSpace(textValue))
        {
            var feature = new CharacterFeature(category, nameValue, textValue);
            if (!features.Contains(feature)) features.Add(feature);
        }
    }

    private static long PositiveId(long id) => id > 0 ? id :
        throw new ArgumentOutOfRangeException(nameof(id), "The numeric ID must be positive.");

    private async Task<JsonElement> GetJsonAsync(string relativePath, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("Heart of Daggers rejected the session or denied access to this resource.");
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        return document.RootElement.Clone();
    }
}

public sealed record CharacterSnapshot(JsonElement Character, JsonElement? SheetState, IReadOnlyList<CharacterFeature>? Features = null, CharacterInventory? Inventory = null);
public sealed record CharacterFeature(string Category, string Name, string Text);
public sealed record CharacterInventoryItem(string Name, string Description, int Quantity);
public sealed record CharacterInventory(IReadOnlyList<CharacterInventoryItem> Items, int Coins, int Handfuls, int Bags, int Chests);
