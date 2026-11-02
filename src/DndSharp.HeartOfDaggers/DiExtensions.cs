using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DndSharp.HeartOfDaggers.Authentication;

namespace DndSharp.HeartOfDaggers;

public static class DiExtensions
{
    public static IServiceCollection AddHeartOfDaggers(this IServiceCollection services)
    {
        if (services.Any(item => item.ServiceType == typeof(HeartSession)))
            return services;
        services.AddSingleton<HeartSessionTransport>();
        services.AddSingleton<ILoginCodeProvider, ImapLoginCodeProvider>();
        services.AddSingleton(provider => new PasswordlessSignIn(
            provider.GetRequiredService<HeartSessionTransport>().Http,
            provider.GetRequiredService<IConfiguration>(), provider.GetRequiredService<ILoginCodeProvider>(),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PasswordlessSignIn>>()));
        services.AddSingleton<HeartSession>();
        services.AddTransient<SessionAuthenticationHandler>();
        services.AddHttpClient<HeartOfDaggersClient>((provider, client) =>
        {
            client.BaseAddress = new Uri("https://heartofdaggers.com/");
            // Authentication can wait up to two minutes for an email before this request is sent.
            client.Timeout = TimeSpan.FromMinutes(4);
        }).ConfigurePrimaryHttpMessageHandler(provider => new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = true,
            CookieContainer = provider.GetRequiredService<HeartSessionTransport>().Cookies,
            AutomaticDecompression = DecompressionMethods.All
        }).AddHttpMessageHandler<SessionAuthenticationHandler>();
        services.AddTransient<CampaignHistoryWatcher>();
        services.AddTransient<CampaignInvitationService>();
        return services;
    }
}
