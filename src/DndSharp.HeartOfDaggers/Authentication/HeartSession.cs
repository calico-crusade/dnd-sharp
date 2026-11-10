using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DndSharp.HeartOfDaggers.Authentication;

/// <summary>Shared cookie jar and unlogged HTTP transport for authentication and invitation requests.</summary>
public sealed class HeartSessionTransport : IDisposable
{
    public static readonly Uri Site = new("https://heartofdaggers.com/");
    public CookieContainer Cookies { get; } = new();
    public HttpClient Http { get; }

    public HeartSessionTransport()
    {
        Http = new HttpClient(new HttpClientHandler
        {
            CookieContainer = Cookies,
            UseCookies = true,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            BaseAddress = Site,
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public void Dispose() => Http.Dispose();
}

/// <summary>Loads, verifies, renews and persists the bot account's site session.</summary>
public sealed class HeartSession(
    HeartSessionTransport transport,
    PasswordlessSignIn signIn,
    IConfiguration configuration,
    ILogger<HeartSession> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool initialized;
    private long generation;
    public long Generation => Interlocked.Read(ref generation);

    private string SessionPath => configuration["HeartOfDaggers:SessionPath"] ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DndSharp", "heart-of-daggers-session.json");

    private bool Automatic => !string.IsNullOrWhiteSpace(configuration["HeartOfDaggers:Email"]);

    public async Task EnsureAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            if (initialized)
                return;
            if (!Automatic)
            {
                var cookie = configuration["HeartOfDaggers:Cookie"];
                if (string.IsNullOrWhiteSpace(cookie))
                    throw new InvalidOperationException("Configure HeartOfDaggers:Email and IMAP, or provide HeartOfDaggers:Cookie.");
                foreach (var part in cookie.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var index = part.IndexOf('=');
                    if (index > 0 && part[..index].StartsWith("wordpress_logged_in_", StringComparison.Ordinal))
                        transport.Cookies.Add(HeartSessionTransport.Site, new Cookie(part[..index], part[(index + 1)..], "/") { Secure = true, HttpOnly = true });
                }
                if (!HasLoginCookie())
                    throw new InvalidOperationException("HeartOfDaggers:Cookie must include the WordPress login cookie.");
                initialized = true;
                Interlocked.Increment(ref generation);
                return;
            }

            await LoadAsync(token);
            if (!HasLoginCookie() || !await IsValidAsync(token))
                await SignInAndSaveAsync(token);
            initialized = true;
            Interlocked.Increment(ref generation);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Refresh once if the site rejected a stale session. Returns false for authorization failures.</summary>
    public async Task<bool> RefreshIfExpiredAsync(long observedGeneration, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            if (Generation != observedGeneration)
                return true;
            if (!Automatic)
                return false;
            if (HasLoginCookie() && await IsValidAsync(token))
                return false;
            await SignInAndSaveAsync(token);
            initialized = true;
            Interlocked.Increment(ref generation);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool HasLoginCookie() => transport.Cookies.GetCookies(HeartSessionTransport.Site).Cast<Cookie>()
        .Any(cookie => cookie.Name.StartsWith("wordpress_logged_in_", StringComparison.Ordinal) && !cookie.Expired);

    private async Task<bool> IsValidAsync(CancellationToken token)
    {
        logger.LogInformation("Checking the saved Heart of Daggers session.");
        try
        {
            return await IsValidAsync(transport.Http, token);
        }
        catch (TimeoutException)
        {
            logger.LogWarning("The Heart of Daggers session check timed out. Check the site's availability and outbound HTTPS connectivity from the bot host.");
            throw;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("The Heart of Daggers session check failed (HTTP error: {HttpError}, status: {StatusCode}).",
                ex.HttpRequestError, ex.StatusCode);
            throw;
        }
    }

    internal static async Task<bool> IsValidAsync(HttpClient http, CancellationToken token)
    {
        try
        {
            using var response = await http.GetAsync("wp-json/dh/v1/characters?per_page=1", token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.Redirect)
                return false;
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: token);
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Heart of Daggers returned an unexpected response while checking the session.");
            return true;
        }
        catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out while checking the Heart of Daggers session.", ex);
        }
    }

    private async Task SignInAndSaveAsync(CancellationToken token)
    {
        logger.LogInformation("Signing in to Heart of Daggers using the bot mailbox.");
        foreach (Cookie cookie in transport.Cookies.GetCookies(HeartSessionTransport.Site))
            if (cookie.Name.StartsWith("wordpress_logged_in_", StringComparison.Ordinal))
                cookie.Expired = true;
        try
        {
            await signIn.SignInAsync(token);
        }
        catch (MailboxSetupException ex)
        {
            logger.LogWarning("Heart of Daggers mailbox setup failed ({Failure}): {Reason}", ex.Failure, ex.Message);
            throw;
        }
        if (!HasLoginCookie() || !await IsValidAsync(token))
            throw new UnauthorizedAccessException("Heart of Daggers did not establish an authenticated session.");
        await SaveAsync(token);
    }

    private async Task LoadAsync(CancellationToken token)
    {
        if (!File.Exists(SessionPath))
            return;
        await using var file = File.OpenRead(SessionPath);
        using var document = await JsonDocument.ParseAsync(file, cancellationToken: token);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            return;
        var saved = document.RootElement.Deserialize<SavedSession>();
        if (saved is null || !string.Equals(saved.Email, configuration["HeartOfDaggers:Email"]?.Trim(), StringComparison.OrdinalIgnoreCase))
            return;
        foreach (var cookie in saved.Cookies ?? [])
        {
            if (!cookie.Name.StartsWith("wordpress_logged_in_", StringComparison.Ordinal) ||
                (cookie.Expires != DateTime.MinValue && cookie.Expires.ToUniversalTime() <= DateTime.UtcNow))
                continue;
            var restored = new Cookie(cookie.Name, cookie.Value, "/") { Secure = true, HttpOnly = true };
            if (cookie.Expires != DateTime.MinValue)
                restored.Expires = cookie.Expires;
            transport.Cookies.Add(HeartSessionTransport.Site, restored);
        }
    }

    private async Task SaveAsync(CancellationToken token)
    {
        var cookies = transport.Cookies.GetCookies(HeartSessionTransport.Site).Cast<Cookie>()
            .Where(cookie => cookie.Name.StartsWith("wordpress_logged_in_", StringComparison.Ordinal) && !cookie.Expired)
            .Select(cookie => new SavedCookie(cookie.Name, cookie.Value, cookie.Expires)).ToArray();
        var path = Path.GetFullPath(SessionPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var file = new FileStream(temporary, options))
                await JsonSerializer.SerializeAsync(file, new SavedSession(configuration["HeartOfDaggers:Email"]!.Trim(), cookies), cancellationToken: token);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private sealed record SavedCookie(string Name, string Value, DateTime Expires);
    private sealed record SavedSession(string Email, SavedCookie[] Cookies);
}
