using System.Net;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using DndSharp.HeartOfDaggers;
using DndSharp.Database;
using Microsoft.Extensions.Logging;

namespace DndSharp.Discord;

/// <summary>Runs one campaign history watcher for each subscribed Discord channel.</summary>
public sealed class CampaignSubscriptions(
    CampaignInvitationService invitations,
    CampaignHistoryWatcher watcher,
    CampaignSubscriptionStore store,
    DiscordSocketClient discord,
    ILogger<CampaignSubscriptions> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<(ulong Guild, ulong Channel, long Campaign), Worker> workers = [];
    private readonly List<CampaignSubscription> subscriptions = [];
    private CancellationTokenSource? lifetime;

    public async Task StartAsync(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            if (lifetime is not null) return;
            var saved = await store.LoadAsync(token);
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
            subscriptions.AddRange(saved);
            foreach (var item in subscriptions) StartWorker(item);
        }
        finally { gate.Release(); }
    }

    public async Task<(CampaignSubscription Subscription, bool Added)> SubscribeAsync(
        ulong guildId, ulong channelId, string invitation, CancellationToken token)
    {
        if (guildId == 0 || channelId == 0) throw new ArgumentException("A server and channel are required.");
        // Reject calls during startup before joining the external campaign.
        await gate.WaitAsync(token);
        try { if (lifetime is null || lifetime.IsCancellationRequested) throw new InvalidOperationException("The bot is starting or stopping."); }
        finally { gate.Release(); }
        var campaign = await invitations.JoinWithoutCharacterAsync(invitation, token);
        var item = new CampaignSubscription(guildId, channelId, campaign.Id, campaign.Slug);
        await gate.WaitAsync(token);
        try
        {
            if (lifetime is null || lifetime.IsCancellationRequested) throw new InvalidOperationException("The bot is stopping.");
            if (subscriptions.Any(existing => Key(existing) == Key(item))) return (item, false);
            item = item with { Duality = subscriptions.FirstOrDefault(existing => existing.GuildId == guildId && existing.CampaignId == item.CampaignId)?.Duality };
            var updated = subscriptions.Append(item).ToArray();
            await store.SaveAsync(updated, token);
            subscriptions.Add(item);
            StartWorker(item);
            return (item, true);
        }
        finally { gate.Release(); }
    }

    public async Task<bool> UnsubscribeAsync(ulong guildId, ulong channelId, string campaign, CancellationToken token)
    {
        var slug = CampaignSession.ParseCampaignSlug(campaign);
        Worker? worker = null;
        await gate.WaitAsync(token);
        try
        {
            var item = subscriptions.FirstOrDefault(item => item.GuildId == guildId && item.ChannelId == channelId &&
                item.CampaignSlug.Equals(slug, StringComparison.OrdinalIgnoreCase));
            if (item is null) return false;
            await store.SaveAsync(subscriptions.Where(existing => existing != item).ToArray(), token);
            subscriptions.Remove(item);
            if (workers.Remove(Key(item), out worker)) worker.Cancellation.Cancel();
        }
        finally { gate.Release(); }
        if (worker is not null)
        {
            try { await worker.Task; } catch (OperationCanceledException) { }
            worker.Cancellation.Dispose();
        }
        return true;
    }

    public async Task<IReadOnlyList<CampaignSubscription>> ListAsync(ulong guildId, ulong channelId, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { return subscriptions.Where(item => item.GuildId == guildId && item.ChannelId == channelId).ToArray(); }
        finally { gate.Release(); }
    }

    public async Task<CampaignSubscription?> FindAsync(ulong guildId, string campaign, CancellationToken token)
    {
        var slug = CampaignSession.ParseCampaignSlug(campaign);
        await gate.WaitAsync(token);
        try { return subscriptions.FirstOrDefault(item => item.GuildId == guildId && item.CampaignSlug.Equals(slug, StringComparison.OrdinalIgnoreCase)); }
        finally { gate.Release(); }
    }

    public async Task<IReadOnlyList<CampaignSubscription>> ListCampaignsAsync(ulong guildId, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { return subscriptions.Where(item => item.GuildId == guildId).DistinctBy(item => item.CampaignId).ToArray(); }
        finally { gate.Release(); }
    }

    public async Task<IReadOnlyList<CampaignSubscription>> ListCampaignSubscriptionsAsync(ulong guildId, long campaignId, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { return subscriptions.Where(item => item.GuildId == guildId && item.CampaignId == campaignId).ToArray(); }
        finally { gate.Release(); }
    }

    public async Task<DualityDisplaySettings?> ConfigureDualityAsync(ulong guildId, string campaign,
        Func<DualityDisplaySettings, DualityDisplaySettings> update, CancellationToken token)
    {
        var slug = CampaignSession.ParseCampaignSlug(campaign);
        await gate.WaitAsync(token);
        try
        {
            var current = subscriptions.FirstOrDefault(item => item.GuildId == guildId &&
                item.CampaignSlug.Equals(slug, StringComparison.OrdinalIgnoreCase));
            if (current is null) return null;
            var settings = update(current.Duality ?? new DualityDisplaySettings());
            var updated = subscriptions.Select(item => item.GuildId == guildId && item.CampaignId == current.CampaignId
                ? item with { Duality = settings } : item).ToArray();
            await store.SaveAsync(updated, token);
            subscriptions.Clear();
            subscriptions.AddRange(updated);
            return settings;
        }
        finally { gate.Release(); }
    }

    public async Task StopAsync()
    {
        Worker[] active;
        await gate.WaitAsync();
        try
        {
            lifetime?.Cancel();
            active = workers.Values.ToArray();
            workers.Clear();
        }
        finally { gate.Release(); }
        try { await Task.WhenAll(active.Select(worker => worker.Task)); }
        catch (OperationCanceledException) { }
        finally
        {
            foreach (var worker in active) worker.Cancellation.Dispose();
            lifetime?.Dispose();
            lifetime = null;
        }
    }

    private void StartWorker(CampaignSubscription item)
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime!.Token);
        var task = Task.Run(() => RunAsync(item, cancellation.Token), CancellationToken.None);
        workers[Key(item)] = new Worker(cancellation, task);
    }

    private async Task RunAsync(CampaignSubscription item, CancellationToken token)
    {
        var retry = TimeSpan.FromSeconds(30);
        var tracker = new CampaignHistoryTracker(item.CampaignId);
        while (!token.IsCancellationRequested)
        {
            try
            {
                await watcher.WatchAsync(item.CampaignSlug, (entry, ct) => SendAsync(item, entry, ct), token, tracker);
                if (!token.IsCancellationRequested) throw new InvalidOperationException("Campaign watcher stopped unexpectedly.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogWarning("Campaign {CampaignId} watcher failed ({ErrorType}); retrying in {Delay}.", item.CampaignId, ex.GetType().Name, retry);
                try { await Task.Delay(retry, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                retry = TimeSpan.FromSeconds(Math.Min(retry.TotalSeconds * 2, 900));
            }
        }
    }

    private async Task SendAsync(CampaignSubscription subscription, CampaignHistoryItem history, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (discord.GetChannel(subscription.ChannelId) is not SocketGuildChannel channel ||
                    channel.Guild.Id != subscription.GuildId || channel is not IMessageChannel messages)
                    throw new InvalidOperationException("The subscribed Discord channel is unavailable.");
                DualityDisplaySettings? settings;
                await gate.WaitAsync(token);
                try { settings = subscriptions.FirstOrDefault(item => Key(item) == Key(subscription))?.Duality; }
                finally { gate.Release(); }
                var claims = await store.ClaimsAsync(subscription.GuildId, subscription.CampaignId, token);
                var owner = FindOwner(history.Entry, claims);
                var prefix = DualityDisplay.Prefix(history.Entry, settings ?? new DualityDisplaySettings());
                var emotes = prefix is not null && ServerEmojiResolver.HasTokens(prefix) ? ServerEmojiResolver.FromGuilds(discord.Guilds) : null;
                await messages.SendMessageAsync(Format(history.Entry, settings, owner, emotes), allowedMentions: AllowedMentions.None,
                    flags: owner.HasValue ? MessageFlags.SuppressNotification : MessageFlags.None);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Could not deliver a campaign history entry to channel {ChannelId} ({ErrorType}); retrying.",
                    subscription.ChannelId, ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(15), token);
            }
        }
        token.ThrowIfCancellationRequested();
    }

    public static ulong? FindOwner(JsonElement entry, IReadOnlyList<CharacterClaim> claims)
    {
        if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("actorPostId", out var actor) ||
            actor.ValueKind != JsonValueKind.Number || !actor.TryGetInt64(out var id) || id <= 0) return null;
        return claims.FirstOrDefault(claim => claim.CharacterId == id)?.UserId;
    }

    public static string Format(JsonElement entry, DualityDisplaySettings? settings = null, ulong? owner = null) =>
        Format(entry, settings, owner, null);

    internal static string Format(JsonElement entry, DualityDisplaySettings? settings, ulong? owner, IReadOnlyCollection<ServerEmoji>? emotes)
    {
        static string? Read(JsonElement value, string key) => value.TryGetProperty(key, out var part) && part.ValueKind == JsonValueKind.String ? part.GetString() : null;
        static string Safe(string? value, string fallback)
        {
            var decoded = WebUtility.HtmlDecode(value);
            var text = string.IsNullOrWhiteSpace(decoded) ? fallback : decoded.Trim();
            return global::Discord.Format.Sanitize(text.Length > 400 ? text[..400] : text);
        }
        var actor = owner is > 0 ? $"<@{owner.Value}>" : Safe(Read(entry, "actorName") ?? Read(entry, "actorLabel"), "Someone");
        var label = Safe(Read(entry, "label"), "Roll");
        var notation = Safe(Read(entry, "notation") ?? Read(entry, "baseNotation"), "");
        var prefix = DualityDisplay.Prefix(entry, settings ?? new DualityDisplaySettings()) ?? "🎲";
        if (emotes is not null) prefix = ServerEmojiResolver.Resolve(prefix, emotes, strict: false);
        return string.IsNullOrEmpty(notation) ? $"{prefix} **{actor}** — {label}" : $"{prefix} **{actor}** — {label}\n{notation}";
    }

    private static (ulong, ulong, long) Key(CampaignSubscription item) => (item.GuildId, item.ChannelId, item.CampaignId);
    private sealed record Worker(CancellationTokenSource Cancellation, Task Task);
}
