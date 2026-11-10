using System.Net;
using System.Text;
using DndSharp.HeartOfDaggers.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using DndSharp.HeartOfDaggers.Verbs;
using MimeKit;

namespace DndSharp.Tests;

[TestClass]
public sealed class HeartAuthenticationTests
{
    [TestMethod]
    public async Task ValidatesSessionUsingCurrentCharacterEndpoint()
    {
        var handler = new SessionCheckHandler(HttpStatusCode.OK, """{"items":[],"page":1}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://heartofdaggers.com/") };
        Assert.IsTrue(await HeartSession.IsValidAsync(http, CancellationToken.None));
        Assert.AreEqual("/wp-json/dh/v1/characters", handler.Path);

        using var expired = new HttpClient(new SessionCheckHandler(HttpStatusCode.Unauthorized, "")) { BaseAddress = http.BaseAddress };
        Assert.IsFalse(await HeartSession.IsValidAsync(expired, CancellationToken.None));
        using var missing = new HttpClient(new SessionCheckHandler(HttpStatusCode.NotFound, "")) { BaseAddress = http.BaseAddress };
        await Assert.ThrowsAsync<HttpRequestException>(() => HeartSession.IsValidAsync(missing, CancellationToken.None));
        using var invalid = new HttpClient(new SessionCheckHandler(HttpStatusCode.OK, """{"unexpected":true}""")) { BaseAddress = http.BaseAddress };
        await Assert.ThrowsAsync<InvalidOperationException>(() => HeartSession.IsValidAsync(invalid, CancellationToken.None));
    }

    [TestMethod]
    public async Task DistinguishesTlsFailuresFromRejectedImapCredentialsWithoutLeakingDetails()
    {
        const string sensitive = "mailbox-password-or-server-response";
        var tls = await Assert.ThrowsAsync<MailboxSetupException>(() => ImapLoginCodeProvider.ConnectAndAuthenticateAsync(
            _ => Task.FromException(new MailKit.Security.SslHandshakeException(sensitive)),
            _ => Task.CompletedTask, NullLogger.Instance, 993, CancellationToken.None));
        Assert.AreEqual(MailboxSetupFailure.TlsHandshake, tls.Failure);
        Assert.IsFalse(tls.ToString().Contains(sensitive, StringComparison.Ordinal));

        var credentials = await Assert.ThrowsAsync<MailboxSetupException>(() => ImapLoginCodeProvider.ConnectAndAuthenticateAsync(
            _ => Task.CompletedTask,
            _ => Task.FromException(new MailKit.Security.AuthenticationException(sensitive)),
            NullLogger.Instance, 993, CancellationToken.None));
        Assert.AreEqual(MailboxSetupFailure.CredentialsRejected, credentials.Failure);
        Assert.IsFalse(credentials.ToString().Contains(sensitive, StringComparison.Ordinal));
        Assert.IsNull(credentials.InnerException);

        var sasl = await Assert.ThrowsAsync<MailboxSetupException>(() => ImapLoginCodeProvider.ConnectAndAuthenticateAsync(
            _ => Task.CompletedTask,
            _ => Task.FromException(new MailKit.Security.SaslException("PLAIN", MailKit.Security.SaslErrorCode.InvalidChallenge, sensitive)),
            NullLogger.Instance, 993, CancellationToken.None));
        Assert.AreEqual(MailboxSetupFailure.SaslAuthentication, sasl.Failure);
        Assert.IsFalse(sasl.ToString().Contains(sensitive, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RejectsInvalidPortInsteadOfSilentlyUsingTheDefault()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HeartOfDaggers:Email"] = "bot@example.test",
            ["HeartOfDaggers:Imap:Host"] = "imap.example.test",
            ["HeartOfDaggers:Imap:Username"] = "bot@example.test",
            ["HeartOfDaggers:Imap:Password"] = "secret",
            ["HeartOfDaggers:Imap:Port"] = "not-a-port"
        }).Build();
        var provider = new ImapLoginCodeProvider(config, NullLogger<ImapLoginCodeProvider>.Instance);
        var error = await Assert.ThrowsAsync<MailboxSetupException>(() => provider.PrepareAsync(CancellationToken.None));
        Assert.AreEqual(MailboxSetupFailure.InvalidPort, error.Failure);
        Assert.IsFalse(error.ToString().Contains("secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task MailboxCheckDoesNotRequestOrReadALoginCode()
    {
        var provider = new FakeCodeProvider();
        var verb = new HeartMailboxCheckVerb(NullLogger<HeartMailboxCheckVerb>.Instance, provider);
        Assert.IsTrue(await verb.Execute(new HeartMailboxCheckOptions(), CancellationToken.None));
        Assert.IsTrue(provider.Prepared);
        Assert.IsTrue(provider.Disposed);
        Assert.IsFalse(provider.CodeRequested);
    }

    [TestMethod]
    public void AcceptsOnlyTheExpectedFreshCodeEmail()
    {
        var message = new MimeMessage { Subject = "Your 2FA Login Code for Heart of Daggers" };
        message.From.Add(MailboxAddress.Parse("noreply@8510680.brevosend.com"));
        message.To.Add(MailboxAddress.Parse("bot@example.test"));
        message.Body = new TextPart("plain") { Text = "Your code is 123456. Reference 987654." };
        Assert.IsTrue(ImapLoginCodeProvider.IsExpectedMessage(message, "bot@example.test", "noreply@8510680.brevosend.com"));
        Assert.IsFalse(ImapLoginCodeProvider.IsExpectedMessage(message, "someone@example.test", "noreply@8510680.brevosend.com"));
        Assert.IsFalse(ImapLoginCodeProvider.IsExpectedMessage(message, "bot@example.test", "attacker@example.test"));
        Assert.AreEqual("123456", ImapLoginCodeProvider.ExtractCode(message));
        message.Subject = "Unrelated message";
        Assert.IsFalse(ImapLoginCodeProvider.IsExpectedMessage(message, "bot@example.test", "noreply@8510680.brevosend.com"));
    }

    [TestMethod]
    public async Task SignsInUsingFreshMailboxCodeAndTheVerifiedForm()
    {
        var provider = new FakeCodeProvider();
        var handler = new LoginHandler(provider);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://heartofdaggers.com/") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HeartOfDaggers:Email"] = "bot@example.test"
        }).Build();
        await new PasswordlessSignIn(http, config, provider).SignInAsync(CancellationToken.None);
        Assert.AreEqual(4, handler.Requests);
        Assert.IsTrue(provider.Disposed);
    }

    private sealed class FakeCodeProvider : ILoginCodeProvider, ILoginCodeAttempt
    {
        public bool Prepared { get; private set; }
        public bool Disposed { get; private set; }
        public bool CodeRequested { get; private set; }
        public Task<ILoginCodeAttempt> PrepareAsync(CancellationToken token)
        {
            Prepared = true;
            return Task.FromResult<ILoginCodeAttempt>(this);
        }
        public Task<string> WaitForCodeAsync(CancellationToken token)
        {
            CodeRequested = true;
            return Task.FromResult("123456");
        }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SessionCheckHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? Path { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private sealed class LoginHandler(FakeCodeProvider provider) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            var path = request.RequestUri!.AbsolutePath;
            string body;
            if (path == "/signin/")
                body = """<script>var pwlessObj = {"ajaxNonce":"fake-nonce"};</script>""";
            else if (path == "/wp-admin/admin-ajax.php")
            {
                Assert.IsTrue(provider.Prepared, "Mailbox baseline must be captured before sending the login email.");
                var form = await request.Content!.ReadAsStringAsync(token);
                StringAssert.Contains(form, "action=passwordless_send_code");
                body = """{"success":true,"data":{"challenge":"fake-challenge"}}""";
            }
            else if (path == "/verify-login/")
                body = """<form id="2fa-form" action="https://heartofdaggers.com/wp-admin/admin-post.php"><input type="hidden" name="action" value="check_2fa_code"><input type="hidden" name="challenge" value="fake-challenge"><input type="hidden" name="_wpnonce" value="fake-nonce"></form>""";
            else if (path == "/wp-admin/admin-post.php")
            {
                var form = await request.Content!.ReadAsStringAsync(token);
                StringAssert.Contains(form, "2fa-code-validator=123456");
                StringAssert.Contains(form, "auth_method=code");
                return new HttpResponseMessage(HttpStatusCode.Redirect);
            }
            else
                throw new InvalidOperationException("Unexpected request.");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8) };
        }
    }
}
