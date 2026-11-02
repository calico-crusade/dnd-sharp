using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using DndSharp.HeartOfDaggers;
using DndSharp.Database;

namespace DndSharp.Discord;

public static class DiExtensions
{
    // Call alongside AddPokemonServices, AddPokeApi, and AddCoreServices.
    public static IServiceCollection AddDiscordBot(this IServiceCollection services)
    {
        services.AddHeartOfDaggers();
        services.AddSingleton(new DiscordSocketConfig
        {
            GatewayIntents = global::Discord.GatewayIntents.Guilds | global::Discord.GatewayIntents.GuildEmojis
        });
        services.AddSingleton<DiscordSocketClient>();
        services.AddSingleton(provider => new InteractionService(
            provider.GetRequiredService<DiscordSocketClient>().Rest,
            new InteractionServiceConfig { DefaultRunMode = RunMode.Async }));
        services.AddSingleton<DiscordBot>();
        services.AddSingleton<CampaignSubscriptionStore>();
        services.AddSingleton<CampaignSubscriptions>();
        services.AddSingleton<CampaignPickerState>();
        services.AddSingleton<DiscordRollService>();
        services.AddSingleton<PokemonCommandService>();
        services.AddHttpClient<IPokemonCardRenderer, PokemonCardRenderer>(client =>
            client.Timeout = TimeSpan.FromSeconds(10));
        services.AddHttpClient<ICharacterSheetRenderer, CharacterSheetRenderer>(client =>
            client.Timeout = TimeSpan.FromSeconds(10)).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false
            });
        return services;
    }
}
