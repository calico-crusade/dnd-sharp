using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace DndSharp.HeartOfDaggers;

/// <summary>The campaign information needed to join its live history feed.</summary>
public sealed class CampaignSession
{
    private readonly Dictionary<long, (string Name, string Path)> characterProfiles = [];
    private CampaignSession(string slug, JsonElement state, string? name = null)
    {
        Slug = slug;
        Name = string.IsNullOrWhiteSpace(name) ? slug : name.Trim();
        Id = RequiredId(state, "campaignId");
        UserId = RequiredId(state, "currentUserId");
        AccessMode = RequiredString(state, "campaignAccessMode");
        if (AccessMode is not ("gm" or "player"))
            throw new FormatException("Unrecognized campaign access mode.");
        SocketAuth = RequiredString(state, "campaignSocketAuth");
        Nonce = RequiredString(state, "nonce");
        if (!Uri.TryCreate(RequiredString(state, "socketUrl"), UriKind.Absolute, out var socketUri) ||
            socketUri.Scheme is not ("https" or "wss") ||
            !socketUri.Host.Equals("realtime.heartofdaggers.com", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("The campaign page returned an unexpected realtime server.");
        SocketUri = socketUri;
        CharacterIds = ReadIds(state, "campaignCharacterIds");
        OwnedCharacterIds = ReadIds(state, "campaignOwnedCharacterIds");
        if (state.TryGetProperty("diceHistory", out var history) && history.ValueKind == JsonValueKind.Array)
            EmbeddedHistory = history.EnumerateArray().Select(item => item.Clone()).ToArray();
    }

    public string Slug { get; }
    public string Name { get; }
    public long Id { get; }
    public long UserId { get; }
    public string AccessMode { get; }
    public IReadOnlyList<long> CharacterIds { get; }
    public IReadOnlyList<long> OwnedCharacterIds { get; }
    public Uri SocketUri { get; }

    public string? CharacterName(long id) => characterProfiles.TryGetValue(id, out var profile) ? profile.Name : null;
    internal string? CharacterPath(long id) => characterProfiles.TryGetValue(id, out var profile) ? profile.Path : null;

    // Credentials are intentionally internal and must never be serialized or logged.
    internal string SocketAuth { get; }
    internal string Nonce { get; }
    internal IReadOnlyList<JsonElement>? EmbeddedHistory { get; }

    public static string ParseCampaignSlug(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var input = value.Trim();
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("heartofdaggers.com", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Use a Heart of Daggers campaign URL or campaign slug.", nameof(value));
            var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !parts[0].Equals("campaign", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Use a Heart of Daggers campaign URL or campaign slug.", nameof(value));
            input = parts[1];
        }
        if (!Regex.IsMatch(input, "^[a-zA-Z0-9_-]{1,80}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Campaign slugs must contain only letters, digits, hyphens or underscores.", nameof(value));
        return input;
    }

    public static CampaignSession Parse(string slug, string html)
    {
        slug = ParseCampaignSlug(slug);
        var state = EmbeddedState.Extract(html, "DH_CAMPAIGN_STATE");
        if (state is null)
            throw new UnauthorizedAccessException("The campaign page did not contain campaign data. Check the URL, access permissions, and Heart of Daggers session cookie.");
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var name = HtmlEntity.DeEntitize(document.DocumentNode.SelectSingleNode("//h1")?.InnerText ?? "").Trim();
        var session = new CampaignSession(slug, state.Value, name);
        session.ReadCharacterProfiles(document);
        return session;
    }

    private void ReadCharacterProfiles(HtmlDocument document)
    {
        var buttons = document.DocumentNode.SelectNodes("//*[@data-dh-character-details-source]") ?? Enumerable.Empty<HtmlNode>();
        foreach (var button in buttons)
        {
            var source = button.GetAttributeValue("data-dh-character-details-source", "");
            var match = Regex.Match(source, @"^dh-campaign-character-details-(\d+)-\d+$", RegexOptions.CultureInvariant);
            if (!match.Success || !long.TryParse(match.Groups[1].Value, out var id) || !CharacterIds.Contains(id))
                continue;
            var shell = button.Ancestors("div").FirstOrDefault(node => node.GetClasses().Contains("dh-campaign-character-shell"));
            var link = shell?.SelectSingleNode(".//a[contains(concat(' ', normalize-space(@class), ' '), ' dh-campaign-character-profile__name ')]");
            var href = WebUtility.HtmlDecode(link?.GetAttributeValue("href", ""));
            if (!Uri.TryCreate(href, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
                !uri.Host.Equals("heartofdaggers.com", StringComparison.OrdinalIgnoreCase) || uri.Port != 443 ||
                uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                continue;
            var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || parts[0] != "character" || !parts[1].All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
                continue;
            var name = WebUtility.HtmlDecode(button.GetAttributeValue("data-character-name", "")).Trim();
            if (name.Length == 0)
                name = WebUtility.HtmlDecode(link!.InnerText).Trim();
            characterProfiles[id] = (name, uri.AbsolutePath);
        }
    }

    private static string RequiredString(JsonElement state, string name) =>
        state.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()! : throw new FormatException($"The campaign state is missing {name}.");

    private static long RequiredId(JsonElement state, string name) =>
        state.TryGetProperty(name, out var value) && value.TryGetInt64(out var id) && id > 0
            ? id : throw new FormatException($"The campaign state is missing {name}.");

    private static long[] ReadIds(JsonElement state, string name)
    {
        if (!state.TryGetProperty(name, out var value))
            return [];
        var ids = new HashSet<long>();
        void Walk(JsonElement item)
        {
            if (item.ValueKind == JsonValueKind.Array)
                foreach (var child in item.EnumerateArray())
                    Walk(child);
            else if (item.ValueKind == JsonValueKind.Object)
                foreach (var property in item.EnumerateObject())
                    Walk(property.Value);
            else if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var number) && number > 0)
                ids.Add(number);
            else if (item.ValueKind == JsonValueKind.String && long.TryParse(item.GetString(), out var parsed) && parsed > 0)
                ids.Add(parsed);
        }
        Walk(value);
        return ids.ToArray();
    }
}
