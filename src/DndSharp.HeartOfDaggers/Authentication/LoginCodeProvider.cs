using System.Net;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace DndSharp.HeartOfDaggers.Authentication;

public interface ILoginCodeAttempt : IAsyncDisposable
{
    Task<string> WaitForCodeAsync(CancellationToken cancellationToken);
}

public interface ILoginCodeProvider
{
    // Capture the mailbox position before requesting a code to avoid reusing an old message.
    Task<ILoginCodeAttempt> PrepareAsync(CancellationToken cancellationToken);
}

/// <summary>Reads a fresh login code from a dedicated Mailcow-compatible IMAP mailbox.</summary>
public sealed class ImapLoginCodeProvider(IConfiguration configuration, ILogger<ImapLoginCodeProvider> logger) : ILoginCodeProvider
{
    public async Task<ILoginCodeAttempt> PrepareAsync(CancellationToken cancellationToken)
    {
        var host = Required("HeartOfDaggers:Imap:Host");
        var username = Required("HeartOfDaggers:Imap:Username");
        var password = Required("HeartOfDaggers:Imap:Password");
        var loginEmail = Required("HeartOfDaggers:Email");
        var sender = configuration["HeartOfDaggers:Imap:Sender"] ?? "noreply@heartofdaggers.com,noreply@8510680.brevosend.com";
        var port = int.TryParse(configuration["HeartOfDaggers:Imap:Port"], out var configuredPort) ? configuredPort : 993;
        if (port is < 1 or > 65535 || string.IsNullOrWhiteSpace(sender))
            throw new InvalidOperationException("Heart of Daggers IMAP settings are invalid.");

        var client = new ImapClient();
        try
        {
            await client.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect, cancellationToken);
            await client.AuthenticateAsync(username, password, cancellationToken);
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
            var existing = await inbox.SearchAsync(SearchQuery.All, cancellationToken);
            var lastId = existing.Count == 0 ? 0u : existing.Max(id => id.Id);
            return new Attempt(client, inbox, inbox.UidValidity, lastId, loginEmail, sender, logger);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private string Required(string key) => !string.IsNullOrWhiteSpace(configuration[key])
        ? configuration[key]! : throw new InvalidOperationException($"Configure {key} for automatic Heart of Daggers login.");

    internal static bool IsExpectedMessage(MimeMessage message, string recipient, string expectedSender)
    {
        var allowedSenders = expectedSender.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var fromSite = message.From.Mailboxes.Any(sender =>
            allowedSenders.Contains(sender.Address, StringComparer.OrdinalIgnoreCase));
        var addressedToBot = message.To.Mailboxes.Concat(message.Cc.Mailboxes)
            .Any(address => address.Address.Equals(recipient, StringComparison.OrdinalIgnoreCase));
        var subject = message.Subject ?? "";
        return fromSite && addressedToBot && subject.Contains("2FA Login Code for Heart of Daggers", StringComparison.OrdinalIgnoreCase);
    }

    internal static string? ExtractCode(MimeMessage message)
    {
        var body = message.TextBody;
        if (string.IsNullOrWhiteSpace(body))
        {
            var html = Regex.Replace(message.HtmlBody ?? "", "<(style|script)\\b[^>]*>[\\s\\S]*?</\\1>", " ", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            body = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ", RegexOptions.None, TimeSpan.FromSeconds(1)));
        }
        var matches = Regex.Matches(body, @"(?<!\d)\d{6}(?!\d)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (matches.Count == 1) return matches[0].Value;
        var contextual = Regex.Match(body, @"(?:code|verification|one.time)[^\d]{0,100}(\d{6})(?!\d)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return contextual.Success ? contextual.Groups[1].Value : null;
    }

    private sealed class Attempt(ImapClient client, IMailFolder inbox, uint validity, uint lastId, string recipient, string sender,
        ILogger logger)
        : ILoginCodeAttempt
    {
        public async Task<string> WaitForCodeAsync(CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            try
            {
                while (true)
                {
                    await client.NoOpAsync(timeout.Token);
                    if (inbox.UidValidity != validity)
                        throw new InvalidOperationException("The mailbox changed while waiting for the login code; start a new login.");
                    var all = await inbox.SearchAsync(SearchQuery.All, timeout.Token);
                    foreach (var uid in all.Where(id => id.Id > lastId).OrderBy(id => id.Id))
                    {
                        logger.LogDebug("Checking a new message in the bot mailbox.");
                        lastId = uid.Id;
                        var summary = await inbox.FetchAsync(new[] { uid }, MessageSummaryItems.Envelope, timeout.Token);
                        if (summary.Count == 0) continue;
                        // Check headers before downloading the message body.
                        var envelope = summary[0].Envelope;
                        if (envelope is null) continue;
                        var headers = new MimeMessage { Subject = envelope.Subject ?? "" };
                        headers.From.AddRange(envelope.From);
                        headers.To.AddRange(envelope.To);
                        headers.Cc.AddRange(envelope.Cc);
                        if (!IsExpectedMessage(headers, recipient, sender))
                        {
                            logger.LogDebug("The message does not match the configured login sender, recipient, and subject.");
                            continue;
                        }
                        var message = await inbox.GetMessageAsync(uid, timeout.Token);
                        var code = ExtractCode(message);
                        if (code is not null) return code;
                        logger.LogWarning("A Heart of Daggers login email was received, but its code could not be extracted.");
                    }
                    await Task.Delay(TimeSpan.FromSeconds(3), timeout.Token);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Timed out waiting for the Heart of Daggers login email.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (client.IsConnected) await client.DisconnectAsync(true);
            client.Dispose();
        }
    }
}
