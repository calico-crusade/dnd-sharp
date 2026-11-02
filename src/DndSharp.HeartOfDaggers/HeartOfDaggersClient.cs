using System.Net;
using System.Text.Json;
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
            var sheet = await GetCharacterSheetStateAsync(fullCharacter, token);
            details.Add(new CharacterSnapshot(EmbeddedState.RemoveCredentials(fullCharacter), sheet.State, sheet.Features));
        }
        return details;
    }

    private async Task<(JsonElement? State, IReadOnlyList<CharacterFeature> Features)> GetCharacterSheetStateAsync(JsonElement character, CancellationToken token)
    {
        if (!character.TryGetProperty("fullSheetUrl", out var value) || value.ValueKind != JsonValueKind.String ||
            !Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("heartofdaggers.com", StringComparison.OrdinalIgnoreCase)) return (null, []);
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !parts[0].Equals("character", StringComparison.OrdinalIgnoreCase) ||
            !parts[1].All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) return (null, []);

        using var response = await http.GetAsync($"character/{parts[1]}/", token);
        // Some campaign members may not share their full sheet with the caller.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Redirect)
            return (null, []);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(token);
        var state = EmbeddedState.Extract(html, "DH_CHARACTER_STATE");
        return (state is null ? null : EmbeddedState.RemoveCredentials(state.Value), ParseFeatures(html));
    }

    internal static IReadOnlyList<CharacterFeature> ParseFeatures(string html)
    {
        var document = new HtmlDocument(); document.LoadHtml(html);
        var features = new List<CharacterFeature>();
        var panels = document.DocumentNode.SelectNodes("//*[contains(concat(' ', normalize-space(@class), ' '), ' dhc-feature-panel ')]") ?? Enumerable.Empty<HtmlNode>();
        foreach (var panel in panels)
        {
            var category = WebUtility.HtmlDecode(panel.GetAttributeValue("data-acc-title", ""));
            if (category is not ("Class" or "Subclass" or "Ancestry & Community")) continue;
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

public sealed record CharacterSnapshot(JsonElement Character, JsonElement? SheetState, IReadOnlyList<CharacterFeature>? Features = null);
public sealed record CharacterFeature(string Category, string Name, string Text);
