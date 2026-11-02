using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;

namespace DndSharp.HeartOfDaggers.Authentication;

/// <summary>Uses the site's email-code login forms without requiring an interactive browser.</summary>
public sealed class PasswordlessSignIn(HttpClient http, IConfiguration configuration, ILoginCodeProvider codes)
{
    public async Task SignInAsync(CancellationToken cancellationToken)
    {
        var email = configuration["HeartOfDaggers:Email"];
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Configure HeartOfDaggers:Email and IMAP settings for automatic login.");

        using var loginPage = await http.GetAsync("signin/", cancellationToken);
        loginPage.EnsureSuccessStatusCode();
        var html = await loginPage.Content.ReadAsStringAsync(cancellationToken);
        var match = Regex.Match(html, @"\bpwlessObj\s*=\s*(\{[^;\r\n]*\})", RegexOptions.CultureInvariant);
        if (!match.Success) throw new InvalidOperationException("The Heart of Daggers login form has changed.");
        using var loginData = JsonDocument.Parse(match.Groups[1].Value);
        if (!loginData.RootElement.TryGetProperty("ajaxNonce", out var nonceValue) ||
            string.IsNullOrWhiteSpace(nonceValue.GetString()))
            throw new InvalidOperationException("The Heart of Daggers login form is missing its security value.");

        // Keep the mailbox open and capture its high-water mark before triggering the email.
        await using var attempt = await codes.PrepareAsync(cancellationToken);
        using var sendForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["action"] = "passwordless_send_code",
            ["security"] = nonceValue.GetString()!,
            ["email"] = email.Trim(),
            ["redirect_to"] = ""
        });
        using var sent = await http.PostAsync("wp-admin/admin-ajax.php", sendForm, cancellationToken);
        sent.EnsureSuccessStatusCode();
        await using var sentStream = await sent.Content.ReadAsStreamAsync(cancellationToken);
        using var result = await JsonDocument.ParseAsync(sentStream, cancellationToken: cancellationToken);
        if (!result.RootElement.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True ||
            !result.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("challenge", out var challengeValue) || challengeValue.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(challengeValue.GetString()))
            throw new InvalidOperationException("Heart of Daggers did not issue an email login challenge.");

        var challenge = challengeValue.GetString()!;
        using var verifyPage = await http.GetAsync("verify-login/?challenge=" + Uri.EscapeDataString(challenge), cancellationToken);
        verifyPage.EnsureSuccessStatusCode();
        var verifyHtml = await verifyPage.Content.ReadAsStringAsync(cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(verifyHtml);
        var form = document.GetElementbyId("2fa-form")
            ?? throw new InvalidOperationException("The Heart of Daggers verification form has changed.");
        if (!Uri.TryCreate(http.BaseAddress, form.GetAttributeValue("action", ""), out var actionUri) ||
            actionUri.Scheme != Uri.UriSchemeHttps || !actionUri.Host.Equals("heartofdaggers.com", StringComparison.OrdinalIgnoreCase) ||
            actionUri.AbsolutePath != "/wp-admin/admin-post.php")
            throw new InvalidOperationException("The Heart of Daggers verification form has an unexpected destination.");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var input in form.SelectNodes(".//input[@type='hidden'][@name]") ?? Enumerable.Empty<HtmlNode>())
            fields[input.GetAttributeValue("name", "")] = WebUtility.HtmlDecode(input.GetAttributeValue("value", ""));
        if (!fields.TryGetValue("action", out var action) || action != "check_2fa_code" ||
            !fields.TryGetValue("challenge", out var formChallenge) || formChallenge != challenge || !fields.ContainsKey("_wpnonce"))
            throw new InvalidOperationException("The Heart of Daggers verification form is invalid.");

        var code = await attempt.WaitForCodeAsync(cancellationToken);
        if (!Regex.IsMatch(code, @"^\d{6}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("The mailbox returned an invalid login code.");
        fields["2fa-code-validator"] = code;
        fields["auth_method"] = "code";
        fields["redirect_to"] = "";
        using var verifyForm = new FormUrlEncodedContent(fields);
        using var verified = await http.PostAsync(actionUri, verifyForm, cancellationToken);
        // A successful form normally redirects; the session manager validates the resulting cookie separately.
        if ((int)verified.StatusCode >= 400)
            throw new InvalidOperationException("Heart of Daggers rejected the login verification.");
    }
}
