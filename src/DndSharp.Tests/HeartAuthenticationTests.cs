using System.Net;
using System.Text;
using DndSharp.HeartOfDaggers.Authentication;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace DndSharp.Tests;

[TestClass]
public sealed class HeartAuthenticationTests
{
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
        public Task<ILoginCodeAttempt> PrepareAsync(CancellationToken token)
        {
            Prepared = true;
            return Task.FromResult<ILoginCodeAttempt>(this);
        }
        public Task<string> WaitForCodeAsync(CancellationToken token) => Task.FromResult("123456");
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    private sealed class LoginHandler(FakeCodeProvider provider) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            var path = request.RequestUri!.AbsolutePath;
            string body;
            if (path == "/signin/") body = """<script>var pwlessObj = {"ajaxNonce":"fake-nonce"};</script>""";
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
            else throw new InvalidOperationException("Unexpected request.");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8) };
        }
    }
}
