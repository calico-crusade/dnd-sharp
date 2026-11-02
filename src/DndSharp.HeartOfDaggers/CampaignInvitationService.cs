using System.Net;
using System.Text.RegularExpressions;
using DndSharp.HeartOfDaggers.Authentication;
using HtmlAgilityPack;

namespace DndSharp.HeartOfDaggers;

/// <summary>Accepts a Heart of Daggers invitation without selecting or linking a character.</summary>
public sealed class CampaignInvitationService(HeartSession session, HeartSessionTransport transport, HeartOfDaggersClient api)
{
    public async Task<CampaignSession> JoinWithoutCharacterAsync(string inviteUrl, CancellationToken cancellationToken = default)
    {
        var invite = ParseInvite(inviteUrl);
        await session.EnsureAsync(cancellationToken);
        var generation = session.Generation;
        using var page = await transport.Http.GetAsync(invite.Uri, cancellationToken);
        if (page.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ||
            page.StatusCode == HttpStatusCode.Redirect && IsLoginRedirect(page.Headers.Location))
        {
            if (await session.RefreshIfExpiredAsync(generation, cancellationToken))
                return await JoinWithoutCharacterAsync(inviteUrl, cancellationToken);
            throw new UnauthorizedAccessException("Heart of Daggers denied access to the invitation.");
        }
        if (page.StatusCode == HttpStatusCode.Redirect)
            return await api.GetCampaignAsync(invite.Slug, cancellationToken);
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync(cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var form = document.DocumentNode.SelectSingleNode("//form[@data-dh-join-no-char-form]");
        if (form is null)
        {
            // Already a member: only report success if the authenticated campaign is accessible.
            return await api.GetCampaignAsync(invite.Slug, cancellationToken);
        }
        if (!form.GetAttributeValue("method", "").Equals("post", StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(invite.Uri, WebUtility.HtmlDecode(form.GetAttributeValue("action", "")), out var destination) ||
            destination.Scheme != Uri.UriSchemeHttps || !destination.Host.Equals("heartofdaggers.com", StringComparison.OrdinalIgnoreCase) ||
            destination.Port != 443 || destination.UserInfo.Length != 0 ||
            CampaignSession.ParseCampaignSlug(destination.GetLeftPart(UriPartial.Path)) != invite.Slug)
            throw new InvalidOperationException("The no-character invitation form has an unexpected destination.");
        var destinationInvite = ParseInvite(destination.AbsoluteUri);
        if (destinationInvite.Token != invite.Token)
            throw new InvalidOperationException("The no-character invitation form has a different invite code.");

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var permitted = new HashSet<string>(StringComparer.Ordinal)
        {
            "dh_campaign_join_no_char_nonce", "_wp_http_referer", "invite", "dh_campaign_join_no_char_submit"
        };
        foreach (var input in form.SelectNodes(".//input[@type='hidden'][@name]") ?? Enumerable.Empty<HtmlNode>())
        {
            var name = input.GetAttributeValue("name", "");
            if (!permitted.Contains(name))
                throw new InvalidOperationException("The no-character invitation form has changed.");
            fields[name] = WebUtility.HtmlDecode(input.GetAttributeValue("value", ""));
        }
        if (!fields.TryGetValue("invite", out var token) || token != invite.Token ||
            !fields.TryGetValue("dh_campaign_join_no_char_nonce", out var nonce) || string.IsNullOrWhiteSpace(nonce) ||
            !fields.TryGetValue("dh_campaign_join_no_char_submit", out var submit) || submit != "1")
            throw new InvalidOperationException("The no-character invitation form is incomplete or invalid.");

        using var body = new FormUrlEncodedContent(fields);
        using var joined = await transport.Http.PostAsync(destinationInvite.Uri, body, cancellationToken);
        if ((int)joined.StatusCode >= 400)
            throw new InvalidOperationException("Heart of Daggers rejected the invitation.");
        return await api.GetCampaignAsync(invite.Slug, cancellationToken);
    }

    public static (Uri Uri, string Slug, string Token) ParseInvite(string input)
    {
        if (!Uri.TryCreate(input?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("heartofdaggers.com", StringComparison.OrdinalIgnoreCase) || uri.Port != 443 || uri.UserInfo.Length != 0)
            throw new ArgumentException("Supply a valid Heart of Daggers campaign invitation URL.", nameof(input));
        var slug = CampaignSession.ParseCampaignSlug(uri.GetLeftPart(UriPartial.Path));
        var values = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2)).Where(part => part.Length == 2 && part[0] == "invite")
            .Select(part => Uri.UnescapeDataString(part[1])).ToArray();
        if (values.Length != 1 || !Regex.IsMatch(values[0], "^[A-Za-z0-9_-]{1,128}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("The campaign invitation is missing a valid invite code.", nameof(input));
        var normalized = new Uri($"https://heartofdaggers.com/campaign/{slug}/?invite={Uri.EscapeDataString(values[0])}");
        return (normalized, slug, values[0]);
    }

    private static bool IsLoginRedirect(Uri? location) => location is not null &&
        (location.ToString().Contains("signin", StringComparison.OrdinalIgnoreCase) ||
         location.ToString().Contains("wp-login", StringComparison.OrdinalIgnoreCase));
}
