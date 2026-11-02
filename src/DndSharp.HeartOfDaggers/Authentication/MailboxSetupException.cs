namespace DndSharp.HeartOfDaggers.Authentication;

public enum MailboxSetupFailure
{
    MissingHost,
    MissingUsername,
    MissingPassword,
    MissingLoginEmail,
    InvalidPort,
    InvalidSender,
    TlsHandshake,
    Connection,
    CredentialsRejected,
    SaslAuthentication,
    InboxAccess,
    LoginCodeTimeout
}

/// <summary>A deliberately sanitized mailbox failure suitable for logs and administrator responses.</summary>
public sealed class MailboxSetupException(MailboxSetupFailure failure) : Exception(Describe(failure))
{
    public MailboxSetupFailure Failure { get; } = failure;

    private static string Describe(MailboxSetupFailure failure) => failure switch
    {
        MailboxSetupFailure.MissingHost => "Configure HeartOfDaggers:Imap:Host.",
        MailboxSetupFailure.MissingUsername => "Configure HeartOfDaggers:Imap:Username with the full mailbox address.",
        MailboxSetupFailure.MissingPassword => "Configure HeartOfDaggers:Imap:Password.",
        MailboxSetupFailure.MissingLoginEmail => "Configure HeartOfDaggers:Email with the Heart of Daggers account email.",
        MailboxSetupFailure.InvalidPort => "HeartOfDaggers:Imap:Port must be a valid IMAP-over-TLS port; Mailcow normally uses 993.",
        MailboxSetupFailure.InvalidSender => "Configure at least one Heart of Daggers login-email sender.",
        MailboxSetupFailure.TlsHandshake => "The IMAP TLS handshake failed. Check the hostname, TLS port, and server certificate chain.",
        MailboxSetupFailure.Connection => "Could not connect to or communicate with the IMAP server. Check the hostname, port, firewall, and network.",
        MailboxSetupFailure.CredentialsRejected => "The IMAP server rejected mailbox authentication. Check the full mailbox username and password, and whether the account or client address is blocked.",
        MailboxSetupFailure.SaslAuthentication => "The IMAP authentication exchange failed. Check the mailbox credentials and the server's supported authentication methods.",
        MailboxSetupFailure.InboxAccess => "The IMAP account authenticated, but its inbox could not be opened or read.",
        MailboxSetupFailure.LoginCodeTimeout => "The mailbox authenticated, but no matching Heart of Daggers login code arrived before the timeout. Check the recipient and configured sender.",
        _ => "The Heart of Daggers mailbox setup failed."
    };
}
