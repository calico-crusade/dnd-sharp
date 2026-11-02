using CardboardBox;
using CommandLine;
using DndSharp.HeartOfDaggers.Authentication;
using Microsoft.Extensions.Logging;

namespace DndSharp.HeartOfDaggers.Verbs;

[Verb("heart-of-daggers-mailbox-check", HelpText = "Check IMAP TLS, credentials, and read-only inbox access without requesting a login email.")]
public sealed class HeartMailboxCheckOptions
{
}

public sealed class HeartMailboxCheckVerb(ILogger<HeartMailboxCheckVerb> logger, ILoginCodeProvider provider)
    : BooleanVerb<HeartMailboxCheckOptions>(logger)
{
    public override async Task<bool> Execute(HeartMailboxCheckOptions options, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            await using var attempt = await provider.PrepareAsync(timeout.Token);
            logger.LogInformation("Mailbox check passed: TLS, authentication, and read-only inbox access succeeded. No login email was requested.");
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Mailbox check timed out. Check the IMAP hostname, port, firewall, and network.");
            return false;
        }
        catch (MailboxSetupException ex)
        {
            logger.LogWarning("Mailbox check failed ({Failure}): {Reason}", ex.Failure, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            // Raw exception messages from mail servers may contain credentials or other sensitive data.
            logger.LogWarning("Mailbox check failed unexpectedly ({ErrorType}).", ex.GetType().FullName);
            return false;
        }
    }
}
