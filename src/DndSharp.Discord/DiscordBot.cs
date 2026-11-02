using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using DndSharp.PokeDex;

namespace DndSharp.Discord;

public sealed class DiscordBot(
    DiscordSocketClient client,
    InteractionService interactions,
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<DiscordBot> logger)
{
    public CancellationToken StoppingToken { get; private set; }

    public async Task<bool> RunAsync(ulong? guildId = null, CancellationToken token = default)
    {
        var secret = configuration["Discord:Token"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogError("Set the Discord:Token configuration value before running the bot.");
            return false;
        }

        var configuredGuild = configuration["Discord:GuildId"];
        if (!guildId.HasValue && !string.IsNullOrWhiteSpace(configuredGuild))
        {
            if (!ulong.TryParse(configuredGuild, out var parsed) || parsed == 0)
            {
                logger.LogError("Discord:GuildId must be a positive server ID, or empty for global commands.");
                return false;
            }
            guildId = parsed;
        }
        if (guildId == 0)
        {
            logger.LogError("The guild ID must be positive.");
            return false;
        }

        try { await services.GetRequiredService<IPokeApiDataInitializer>().PrepareAsync(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return true; }

        StoppingToken = token;
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registrationStarted = 0;

        async Task Ready()
        {
            // Ready also fires on reconnect; registration only needs to happen once per run.
            if (Interlocked.Exchange(ref registrationStarted, 1) != 0) return;

            try
            {
                if (guildId.HasValue)
                    await interactions.RegisterCommandsToGuildAsync(guildId.Value, deleteMissing: false);
                else
                    await interactions.RegisterCommandsGloballyAsync(deleteMissing: false);
                await services.GetRequiredService<CampaignSubscriptions>().StartAsync(token);
                logger.LogInformation("Discord bot ready as {User}. Commands registered in {Scope}.",
                    client.CurrentUser, guildId?.ToString() ?? "global scope");
                registered.TrySetResult();
            }
            catch (Exception ex)
            {
                registered.TrySetException(ex);
            }
        }

        client.Log += Log;
        interactions.Log += Log;
        client.Ready += Ready;
        client.InteractionCreated += HandleInteraction;
        interactions.SlashCommandExecuted += CommandExecuted;
        try
        {
            await interactions.AddModulesAsync(typeof(BotCommands).Assembly, services);
            await client.LoginAsync(TokenType.Bot, secret.Trim());
            await client.StartAsync();
            await registered.Task.WaitAsync(TimeSpan.FromSeconds(60), token);
            await Task.Delay(Timeout.Infinite, token);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return true;
        }
        finally
        {
            client.Ready -= Ready;
            client.InteractionCreated -= HandleInteraction;
            interactions.SlashCommandExecuted -= CommandExecuted;
            await services.GetRequiredService<CampaignSubscriptions>().StopAsync();
            await client.StopAsync();
            await client.LogoutAsync();
            client.Log -= Log;
            interactions.Log -= Log;
        }
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            await interactions.ExecuteCommandAsync(new SocketInteractionContext(client, interaction), services);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not dispatch Discord interaction {Id}.", interaction.Id);
        }
    }

    private async Task CommandExecuted(SlashCommandInfo command, IInteractionContext context, IResult result)
    {
        if (result.IsSuccess) return;
        logger.LogWarning("Discord command {Command} failed: {Error}.", command.Name, result.ErrorReason);
        const string message = "The command could not be completed. Check your options and try again.";
        try
        {
            if (context.Interaction.HasResponded)
                await context.Interaction.ModifyOriginalResponseAsync(response => response.Content = message);
            else
                await context.Interaction.RespondAsync(message, ephemeral: true, allowedMentions: AllowedMentions.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not send the command error response.");
        }
    }

    private Task Log(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            _ => LogLevel.Debug
        };
        logger.Log(level, message.Exception, "Discord {Source}: {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }
}
