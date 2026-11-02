using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using DndSharp.HeartOfDaggers;
using DndSharp.HeartOfDaggers.Authentication;

namespace DndSharp.Discord;

[RequireContext(ContextType.Guild)]
public sealed class CampaignCommands(CampaignSubscriptions subscriptions, HeartOfDaggersClient api, ILogger<CampaignCommands> logger)
    : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("campaign-watch", "Join a Heart of Daggers campaign and post new history here.")]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    [RequireUserPermission(GuildPermission.Administrator)]
    public Task Watch([Summary(description: "Heart of Daggers campaign invitation URL")] string invite) =>
        Execute(async token =>
        {
            var (subscription, added) = await subscriptions.SubscribeAsync(Context.Guild.Id, Context.Channel.Id, invite, token);
            await ModifyOriginalResponseAsync(message => message.Content = added
                ? $"Campaign `{subscription.CampaignSlug}` joined without a character. This channel is subscribed; the watcher is connecting."
                : $"This channel already watches campaign `{subscription.CampaignSlug}`.");
        });

    [SlashCommand("campaign-watches", "List Heart of Daggers campaigns watched in this channel.")]
    public Task List() => Execute(async token =>
    {
        var items = await subscriptions.ListAsync(Context.Guild.Id, Context.Channel.Id, token);
        var names = await Task.WhenAll(items.Take(30).Select(async item =>
        {
            try
            {
                var campaign = await api.GetCampaignAsync(item.CampaignSlug, token);
                var name = campaign.Id == item.CampaignId ? campaign.Name : item.CampaignSlug;
                var safeName = global::Discord.Format.Sanitize(CharacterPickerCommands.Label(name, item.CampaignSlug));
                return $"• {safeName}";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return $"• {item.CampaignSlug}";
            }
        }));
        var suffix = items.Count > 30 ? $"\n…and {items.Count - 30} more." : "";
        var result = items.Count == 0
            ? "This channel is not watching any campaigns."
            : $"Watched campaigns:\n{string.Join("\n", names)}{suffix}";
        await ModifyOriginalResponseAsync(message =>
        {
            message.Content = result.Length > 1900 ? result[..1900] : result;
            message.AllowedMentions = AllowedMentions.None;
        });
    });

    private async Task Execute(Func<CancellationToken, Task> action)
    {
        await DeferAsync(ephemeral: true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await action(timeout.Token);
        }
        catch (ArgumentException)
        {
            await ModifyOriginalResponseAsync(message => message.Content = "Check the campaign URL and the supplied command options, then try again.");
        }
        catch (OperationCanceledException)
        {
            await ModifyOriginalResponseAsync(message => message.Content = "The request timed out. Please try again.");
        }
        catch (MailboxSetupException ex)
        {
            await ModifyOriginalResponseAsync(message => message.Content = ex.Message);
        }
        catch (Exception ex)
        {
            // The invite URL is a secret; do not log exception text or interaction arguments.
            logger.LogWarning("Campaign command failed ({ErrorType}).", ex.GetType().FullName);
            await ModifyOriginalResponseAsync(message => message.Content =
                "Could not update the campaign subscription. Check the invitation and bot account configuration, then try again.");
        }
    }
}
