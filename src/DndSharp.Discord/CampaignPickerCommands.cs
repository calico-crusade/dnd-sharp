using System.Collections.Concurrent;
using System.Text.Json;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DndSharp.Database;
using DndSharp.HeartOfDaggers;
using DndSharp.HeartOfDaggers.Authentication;
using Microsoft.Extensions.Logging;

namespace DndSharp.Discord;

public enum CampaignPickerAction
{
    Unwatch, Emoji, Settings, Characters
}

public sealed record CampaignPickerRequest(ulong GuildId, ulong ChannelId, ulong UserId, CampaignPickerAction Action,
    DateTimeOffset Expires, DualityOutcome Outcome = default, string? Display = null, bool? Enabled = null, int? Difficulty = null,
    bool UsesExternalEmoji = false);

/// <summary>Keeps short-lived command arguments off Discord component IDs.</summary>
public sealed class CampaignPickerState
{
    private readonly ConcurrentDictionary<string, CampaignPickerRequest> requests = new();
    public string Add(CampaignPickerRequest request)
    {
        foreach (var pair in requests)
        if (pair.Value.Expires <= DateTimeOffset.UtcNow)
            requests.TryRemove(pair.Key, out _);
        var id = Guid.NewGuid().ToString("N");
        requests[id] = request;
        return id;
    }
    public CampaignPickerRequest? Get(string id) => requests.TryGetValue(id, out var item) && item.Expires > DateTimeOffset.UtcNow ? item : null;
    public bool Take(string id, out CampaignPickerRequest? item)
    {
        if (!requests.TryRemove(id, out item) || item.Expires <= DateTimeOffset.UtcNow)
        {
            item = null;
            return false;
        }
        return true;
    }
}

[RequireContext(ContextType.Guild)]
public sealed class CampaignPickerCommands(CampaignSubscriptions subscriptions, CampaignSubscriptionStore store,
    HeartOfDaggersClient api, CampaignPickerState state, ILogger<CampaignPickerCommands> logger)
    : InteractionModuleBase<SocketInteractionContext>
{
    private const int PageSize = 25;

    [SlashCommand("campaign-unwatch", "Stop posting a watched campaign's history in this channel.")]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    [RequireUserPermission(GuildPermission.Administrator)]
    public Task Unwatch() => BeginAsync(CampaignPickerAction.Unwatch);

    [SlashCommand("campaign-duality-emoji", "Set a campaign's emoji for a duality outcome.")]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    [RequireUserPermission(GuildPermission.Administrator)]
    public Task DualityEmoji([Summary(description: "Outcome to configure")] DualityOutcome outcome,
        [Summary(description: "Emoji or text to display, up to 100 characters"), MaxLength(100)] string display) =>
        BeginAsync(CampaignPickerAction.Emoji, outcome, display);

    [SlashCommand("campaign-duality-settings", "Configure a campaign's duality display.")]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    [RequireUserPermission(GuildPermission.Administrator)]
    public Task DualitySettings([Summary(description: "Enable special duality displays")] bool? enabled = null,
        [Summary(description: "Default difficulty; use 0 to clear it"), MinValue(0), MaxValue(100)] int? difficulty = null) =>
        BeginAsync(CampaignPickerAction.Settings, enabled: enabled, difficulty: difficulty);

    [SlashCommand("campaign-characters", "List characters in a watched campaign.")]
    public Task Characters() => BeginAsync(CampaignPickerAction.Characters);

    private Task BeginAsync(CampaignPickerAction action, DualityOutcome outcome = default, string? display = null,
        bool? enabled = null, int? difficulty = null) => RunAsync(async token =>
    {
        var usesExternal = false;
        if (action == CampaignPickerAction.Emoji)
        {
            if (string.IsNullOrWhiteSpace(display) || display.Length > 100 || display.Any(char.IsControl))
                throw new EmojiResolutionException("The display must be 1 to 100 characters without line breaks.");
            var emotes = ServerEmojiResolver.FromGuilds(Context.Client.Guilds).ToList();
            if (ServerEmojiResolver.HasTokens(display))
            {
                var appEmotes = await Context.Client.GetApplicationEmotesAsync(new RequestOptions { CancelToken = token });
                emotes.AddRange(appEmotes.Select(emoji => new ServerEmoji(emoji.Name, emoji.Id, emoji.Animated, IsApplication: true)));
            }
            display = ServerEmojiResolver.Resolve(display, emotes, strict: true);
            usesExternal = ServerEmojiResolver.UsesExternalGuildEmoji(display, emotes, Context.Guild.Id);
            if (display.Length > 500)
                throw new EmojiResolutionException("The expanded emoji display is too long. Use fewer custom emoji.");
            _ = DualityDisplay.SetEmoji(new DualityDisplaySettings(), outcome, display);
        }
        var request = new CampaignPickerRequest(Context.Guild.Id, Context.Channel.Id, Context.User.Id, action,
            DateTimeOffset.UtcNow.AddMinutes(10), outcome, display, enabled, difficulty, usesExternal);
        var campaigns = await AvailableAsync(request, token);
        if (campaigns.Count == 0)
        {
            await ClearAsync(EmptyMessage(request));
            return;
        }
        if (campaigns.Count == 1)
        {
            await ExecuteAsync(request, campaigns[0], token);
            return;
        }
        var id = state.Add(request);
        await ShowPickerAsync(id, request, campaigns, 0, token);
    }, component: false);

    [ComponentInteraction("campaign-picker:*")]
    public Task Select(string id, string[] values) => RunAsync(async token =>
    {
        var request = Validate(id);
        if (values.Length != 1 || !long.TryParse(values[0], out var campaignId))
            throw new ArgumentException();
        var campaign = (await AvailableAsync(request, token)).FirstOrDefault(item => item.CampaignId == campaignId);
        if (campaign is null)
        {
            await ClearAsync("That campaign is no longer watched. Run the command again.");
            return;
        }
        if (!state.Take(id, out _))
        {
            await ClearAsync("This selection has expired. Run the command again.");
            return;
        }
        await ExecuteAsync(request, campaign, token);
    });

    [ComponentInteraction("campaign-picker-page:*:*")]
    public Task Page(string id, string page) => RunAsync(async token =>
    {
        var request = Validate(id);
        if (!int.TryParse(page, out var index))
            throw new ArgumentException();
        var campaigns = await AvailableAsync(request, token);
        if (campaigns.Count == 0)
        {
            await ClearAsync(EmptyMessage(request));
            return;
        }
        await ShowPickerAsync(id, request, campaigns, index, token);
    });

    private CampaignPickerRequest Validate(string id)
    {
        var request = state.Get(id) ?? throw new ArgumentException("Selection expired.");
        if (request.UserId != Context.User.Id || request.GuildId != Context.Guild.Id || request.ChannelId != Context.Channel.Id)
            throw new ArgumentException("Selection belongs to another user or channel.");
        if (request.Action != CampaignPickerAction.Characters &&
            (Context.User is not SocketGuildUser user || !user.GuildPermissions.Administrator))
            throw new ArgumentException("Administrator permission is required.");
        return request;
    }

    private Task<IReadOnlyList<CampaignSubscription>> AvailableAsync(CampaignPickerRequest request, CancellationToken token) =>
        request.Action == CampaignPickerAction.Unwatch
            ? subscriptions.ListAsync(request.GuildId, request.ChannelId, token)
            : subscriptions.ListCampaignsAsync(request.GuildId, token);

    private static string EmptyMessage(CampaignPickerRequest request) => request.Action == CampaignPickerAction.Unwatch
        ? "This channel is not watching any campaigns." : "This server is not watching any campaigns.";

    private async Task ShowPickerAsync(string id, CampaignPickerRequest request, IReadOnlyList<CampaignSubscription> campaigns, int page, CancellationToken token)
    {
        var ordered = campaigns.OrderBy(item => item.CampaignSlug, StringComparer.OrdinalIgnoreCase).ToArray();
        var index = Math.Clamp(page, 0, Math.Max(0, (ordered.Length - 1) / PageSize));
        var selected = ordered.Skip(index * PageSize).Take(PageSize).ToArray();
        var names = await Task.WhenAll(selected.Select(async item =>
        {
            try
            {
                var session = await api.GetCampaignAsync(item.CampaignSlug, token);
                return session.Id == item.CampaignId ? session.Name : item.CampaignSlug;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return item.CampaignSlug;
            }
        }));
        var menu = new SelectMenuBuilder().WithCustomId($"campaign-picker:{id}").WithPlaceholder("Choose a campaign");
        for (var i = 0; i < selected.Length; i++)
            menu.AddOption(CharacterPickerCommands.Label(names[i], selected[i].CampaignSlug), selected[i].CampaignId.ToString(), description: $"Campaign: {selected[i].CampaignSlug}");
        var builder = new ComponentBuilder().WithSelectMenu(menu);
        if (ordered.Length > PageSize)
        {
            builder.WithButton("Previous", $"campaign-picker-page:{id}:{index - 1}", disabled: index == 0, row: 1)
                .WithButton("Next", $"campaign-picker-page:{id}:{index + 1}", disabled: (index + 1) * PageSize >= ordered.Length, row: 1);
        }
        await ShowAsync("Choose a campaign.", builder.Build());
    }

    private async Task ExecuteAsync(CampaignPickerRequest request, CampaignSubscription campaign, CancellationToken token)
    {
        switch (request.Action)
        {
            case CampaignPickerAction.Unwatch:
                var removed = await subscriptions.UnsubscribeAsync(request.GuildId, request.ChannelId, campaign.CampaignSlug, token);
                await ClearAsync(removed ? "Stopped watching the campaign in this channel." : "This channel is not watching that campaign.");
                break;
            case CampaignPickerAction.Emoji:
                if (request.UsesExternalEmoji)
                {
                    var watched = await subscriptions.ListCampaignSubscriptionsAsync(request.GuildId, campaign.CampaignId, token);
                    var inaccessible = watched.Any(item => Context.Client.GetChannel(item.ChannelId) is not SocketGuildChannel channel ||
                        channel.Guild.CurrentUser is null || !channel.Guild.CurrentUser.GetPermissions(channel).UseExternalEmojis);
                    if (inaccessible)
                    {
                        await ClearAsync("The bot needs Use External Emoji permission in every channel watching this campaign before it can use that emoji.");
                        break;
                    }
                }
                var emojis = await subscriptions.ConfigureDualityAsync(request.GuildId, campaign.CampaignSlug,
                    current => DualityDisplay.SetEmoji(current, request.Outcome, request.Display!), token);
                await ClearAsync(emojis is null ? "This server is not watching that campaign." : $"Updated the duality display for `{request.Outcome}` across this server's channels watching this campaign. Preview: {request.Display}");
                break;
            case CampaignPickerAction.Settings:
                var settings = await subscriptions.ConfigureDualityAsync(request.GuildId, campaign.CampaignSlug,
                    current => current with { Enabled = request.Enabled ?? current.Enabled, Difficulty = request.Difficulty is null ? current.Difficulty : request.Difficulty == 0 ? null : request.Difficulty }, token);
                await ClearAsync(settings is null ? "This server is not watching that campaign." :
                    $"Duality display: {(settings.Enabled ? "enabled" : "disabled")}; default difficulty: {(settings.Difficulty?.ToString() ?? "none")}. Settings apply to this campaign in all watched channels on this server.");
                break;
            case CampaignPickerAction.Characters:
                await ShowCharactersAsync(request, campaign, token);
                break;
            default:
                throw new ArgumentException("Unknown action.");
        }
    }

    private async Task ShowCharactersAsync(CampaignPickerRequest request, CampaignSubscription campaign, CancellationToken token)
    {
        var session = await api.GetCampaignAsync(campaign.CampaignSlug, token);
        if (session.Id != campaign.CampaignId)
            throw new InvalidOperationException("Campaign identity changed.");
        var claims = await store.ClaimsAsync(request.GuildId, session.Id, token);
        var selected = session.CharacterIds.Take(40).ToArray();
        var lines = selected.Select((id, i) =>
        {
            var name = global::Discord.Format.Sanitize(CharacterPickerCommands.Label(session.CharacterName(id), "Unknown"));
            if (name.Length > 70)
                name = name[..70];
            var owner = claims.FirstOrDefault(claim => claim.CharacterId == id);
            var status = owner is null ? "unclaimed" : owner.UserId == request.UserId ? "yours" : "claimed";
            return $"`{id}` — {name} ({status})";
        }).ToArray();
        var text = lines.Length == 0 ? "No characters are visible in this campaign." : string.Join("\n", lines);
        if (session.CharacterIds.Count > selected.Length)
            text += "\nAdditional characters omitted.";
        await ClearAsync(text.Length > 1900 ? text[..1900] : text);
    }

    private Task ShowAsync(string text, MessageComponent components) => ModifyOriginalResponseAsync(message =>
    {
        message.Content = text;
        message.Components = components;
        message.AllowedMentions = AllowedMentions.None;
    });
    private Task ClearAsync(string text) => ShowAsync(text, new ComponentBuilder().Build());

    private async Task RunAsync(Func<CancellationToken, Task> action, bool component = true)
    {
        await DeferAsync(ephemeral: !component);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await action(timeout.Token);
        }
        catch (EmojiResolutionException ex)
        {
            await ClearAsync(ex.Message);
        }
        catch (MailboxSetupException ex)
        {
            await ClearAsync(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Campaign picker failed ({ErrorType}).", ex.GetType().FullName);
            await ClearAsync("Could not complete the campaign selection. Check the command options and permissions, then try again.");
        }
    }
}
