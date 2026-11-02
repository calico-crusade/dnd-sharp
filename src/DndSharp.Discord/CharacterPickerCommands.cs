using System.Net;
using System.Text.Json;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DndSharp.Database;
using DndSharp.HeartOfDaggers;
using DndSharp.HeartOfDaggers.Authentication;
using Microsoft.Extensions.Logging;

namespace DndSharp.Discord;

/// <summary>A private, paginated campaign and character selector for linking your account.</summary>
[RequireContext(ContextType.Guild)]
public sealed class CharacterPickerCommands(CampaignSubscriptions subscriptions, CampaignSubscriptionStore store,
    HeartOfDaggersClient api, ICharacterSheetRenderer renderer, ILogger<CharacterPickerCommands> logger) : InteractionModuleBase<SocketInteractionContext>
{
    private const int PageSize = 25;

    [SlashCommand("character-claim", "Choose a campaign character to link to your Discord account.")]
    public Task Claim() => BeginAsync("claim");

    [SlashCommand("character-unclaim", "Choose one of your linked characters to unlink.")]
    public Task Unclaim() => BeginAsync("unclaim");

    [SlashCommand("character-sheet", "Generate an image of a campaign character's sheet.")]
    public Task Sheet() => BeginAsync("sheet");

    private Task BeginAsync(string action) => RunAsync(async token =>
    {
        var campaigns = await subscriptions.ListCampaignsAsync(Context.Guild.Id, token);
        if (campaigns.Count == 0) { await ClearAsync("This server is not watching any campaigns."); return; }
        if (campaigns.Count == 1) await ShowCharactersAsync(action, campaigns[0].CampaignId, 0, token);
        else await ShowCampaignsAsync(action, 0, token);
    }, component: false);

    [ComponentInteraction("character-campaign:*:*")]
    public Task ChooseCampaign(string action, string owner, string[] values) => RunAsync(async token =>
    {
        EnsureOwner(owner);
        if (values.Length != 1 || !long.TryParse(values[0], out var campaignId)) throw new ArgumentException();
        ValidateAction(action);
        await ShowCharactersAsync(action, campaignId, 0, token);
    });

    [ComponentInteraction("character-campaign-page:*:*:*")]
    public Task CampaignPage(string action, string owner, string page) => RunAsync(async token =>
    {
        EnsureOwner(owner);
        if (!int.TryParse(page, out var index)) throw new ArgumentException();
        ValidateAction(action);
        await ShowCampaignsAsync(action, index, token);
    });

    [ComponentInteraction("character-choice:*:*:*")]
    public Task ChooseCharacter(string action, string owner, string campaign, string[] values) => RunAsync(async token =>
    {
        EnsureOwner(owner);
        ValidateAction(action);
        if (!long.TryParse(campaign, out var campaignId) || values.Length != 1 ||
            !long.TryParse(values[0], out var characterId) || characterId <= 0) throw new ArgumentException();
        var session = await GetSessionAsync(campaignId, token);
        if (action != "unclaim" && !session.CharacterIds.Contains(characterId)) { await ClearAsync("That character is no longer in this campaign. Run the command again."); return; }
        if (action == "sheet")
        {
            var menu = new SelectMenuBuilder().WithCustomId($"character-sheet-theme:{Context.User.Id}:{campaignId}:{characterId}").WithPlaceholder("Choose a sheet theme");
            foreach (var theme in CharacterSheetThemes.All) menu.AddOption(theme.Label, theme.Id);
            await ShowAsync("Choose a theme for the character sheet.", new ComponentBuilder().WithSelectMenu(menu).Build());
            return;
        }
        if (action == "unclaim")
        {
            var removed = await store.UnclaimAsync(Context.Guild.Id, session.Id, characterId, Context.User.Id, token);
            await ClearAsync(removed ? "Character link removed." : "You have not claimed that character.");
        }
        else
        {
            var claimed = await store.ClaimAsync(Context.Guild.Id, session.Id, characterId, Context.User.Id, token);
            await ClearAsync(claimed ? "Character linked to your Discord account. New rolls will identify you without a notification."
                : "That character is already claimed by another server member.");
        }
    });

    [ComponentInteraction("character-sheet-theme:*:*:*")]
    public Task ChooseTheme(string owner, string campaign, string character, string[] values) => RunAsync(async token =>
    {
        EnsureOwner(owner);
        if (!long.TryParse(campaign, out var campaignId) || !long.TryParse(character, out var characterId) || characterId <= 0 ||
            values.Length != 1 || CharacterSheetThemes.Find(values[0]) is null) throw new ArgumentException();
        var session = await GetSessionAsync(campaignId, token);
        if (!session.CharacterIds.Contains(characterId)) { await ClearAsync("That character is no longer in this campaign. Run the command again."); return; }
        var snapshot = await api.GetCharacterDetailsAsync(characterId, token);
        await using var image = await renderer.RenderAsync(snapshot, values[0], token);
        await FollowupWithFileAsync(image, "character-sheet.png", text: "Character sheet", ephemeral: false, allowedMentions: AllowedMentions.None);
        await ClearAsync("Character sheet posted in this channel.");
    });

    [ComponentInteraction("character-choice-page:*:*:*:*")]
    public Task CharacterPage(string action, string owner, string campaign, string page) => RunAsync(async token =>
    {
        EnsureOwner(owner);
        if (!long.TryParse(campaign, out var campaignId) || !int.TryParse(page, out var index)) throw new ArgumentException();
        ValidateAction(action);
        await ShowCharactersAsync(action, campaignId, index, token);
    });

    private async Task ShowCampaignsAsync(string action, int page, CancellationToken token)
    {
        var campaigns = (await subscriptions.ListCampaignsAsync(Context.Guild.Id, token)).OrderBy(item => item.CampaignSlug, StringComparer.OrdinalIgnoreCase).ToArray();
        if (campaigns.Length == 0) { await ClearAsync("This server is not watching any campaigns."); return; }
        var index = PageIndex(page, campaigns.Length);
        var selected = campaigns.Skip(index * PageSize).Take(PageSize).ToArray();
        var names = await Task.WhenAll(selected.Select(async item =>
        {
            try { var session = await api.GetCampaignAsync(item.CampaignSlug, token); return session.Id == item.CampaignId ? session.Name : item.CampaignSlug; }
            catch (Exception ex) when (ex is not OperationCanceledException) { return item.CampaignSlug; }
        }));
        var menu = new SelectMenuBuilder().WithCustomId($"character-campaign:{action}:{Context.User.Id}").WithPlaceholder("Choose a campaign");
        for (var i = 0; i < selected.Length; i++)
            menu.AddOption(Label(names[i], selected[i].CampaignSlug), selected[i].CampaignId.ToString(), description: $"Campaign: {selected[i].CampaignSlug}");
        var builder = new ComponentBuilder().WithSelectMenu(menu);
        AddPageButtons(builder, index, campaigns.Length, $"character-campaign-page:{action}:{Context.User.Id}");
        await ShowAsync("Choose a campaign.", builder.Build());
    }

    private async Task ShowCharactersAsync(string action, long campaignId, int page, CancellationToken token)
    {
        var session = await GetSessionAsync(campaignId, token);
        var claims = await store.ClaimsAsync(Context.Guild.Id, campaignId, token);
        var ids = (action == "unclaim" ? claims.Where(claim => claim.UserId == Context.User.Id).Select(claim => claim.CharacterId) : session.CharacterIds).Order().ToArray();
        if (ids.Length == 0) { await ClearAsync(action == "unclaim" ? "You have no linked characters in this campaign." : "There are no characters visible in this campaign."); return; }
        var index = PageIndex(page, ids.Length);
        var selected = ids.Skip(index * PageSize).Take(PageSize).ToArray();
        var names = await Task.WhenAll(selected.Select(async id =>
        {
            try
            {
                var response = await api.GetCharacterAsync(id, token);
                return response.TryGetProperty("character", out var character) && character.ValueKind == JsonValueKind.Object &&
                    character.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                    ? name.GetString() : null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { return null; }
        }));
        var menu = new SelectMenuBuilder().WithCustomId($"character-choice:{action}:{Context.User.Id}:{campaignId}").WithPlaceholder(action == "sheet" ? "Choose a character" : "Choose your character");
        for (var i = 0; i < selected.Length; i++)
        {
            var claim = claims.FirstOrDefault(item => item.CharacterId == selected[i]);
            var status = claim is null ? "Available" : claim.UserId == Context.User.Id ? "Already linked to you" : "Already claimed";
            menu.AddOption(Label(names[i], $"Character {selected[i]}"), selected[i].ToString(), description: status);
        }
        var builder = new ComponentBuilder().WithSelectMenu(menu);
        AddPageButtons(builder, index, ids.Length, $"character-choice-page:{action}:{Context.User.Id}:{campaignId}");
        var campaignName = global::Discord.Format.Sanitize(Label(session.Name, session.Slug));
        await ShowAsync($"Choose a character in **{campaignName}**.", builder.Build());
    }

    private async Task<CampaignSession> GetSessionAsync(long campaignId, CancellationToken token)
    {
        var item = (await subscriptions.ListCampaignsAsync(Context.Guild.Id, token)).FirstOrDefault(item => item.CampaignId == campaignId)
            ?? throw new ArgumentException("The campaign is no longer watched.");
        var session = await api.GetCampaignAsync(item.CampaignSlug, token);
        if (session.Id != campaignId) throw new InvalidOperationException("Campaign identity changed.");
        return session;
    }

    internal static int PageIndex(int page, int count) => Math.Clamp(page, 0, Math.Max(0, (count - 1) / PageSize));
    internal static string Label(string? name, string fallback)
    {
        var decoded = WebUtility.HtmlDecode(name)?.Trim();
        var label = string.IsNullOrWhiteSpace(decoded) ? fallback : decoded;
        return label.Length > 100 ? label[..100] : label;
    }

    private static void AddPageButtons(ComponentBuilder builder, int page, int count, string prefix)
    {
        if (count <= PageSize) return;
        builder.WithButton("Previous", $"{prefix}:{page - 1}", disabled: page == 0, row: 1)
            .WithButton("Next", $"{prefix}:{page + 1}", disabled: (page + 1) * PageSize >= count, row: 1);
    }

    private void EnsureOwner(string owner)
    {
        if (!ulong.TryParse(owner, out var userId) || userId != Context.User.Id) throw new ArgumentException("This picker belongs to another user.");
    }

    private static void ValidateAction(string action)
    {
        if (action is not ("claim" or "unclaim" or "sheet")) throw new ArgumentException("Unknown action.");
    }

    private Task ShowAsync(string text, MessageComponent components) => ModifyOriginalResponseAsync(message =>
    {
        message.Content = text; message.Components = components; message.AllowedMentions = AllowedMentions.None;
    });

    private Task ClearAsync(string text) => ShowAsync(text, new ComponentBuilder().Build());

    private async Task RunAsync(Func<CancellationToken, Task> action, bool component = true)
    {
        await DeferAsync(ephemeral: !component);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await action(timeout.Token); }
        catch (MailboxSetupException ex)
        {
            await ClearAsync(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Character picker failed ({ErrorType}).", ex.GetType().FullName);
            await ClearAsync("Could not load or update the character selection. Please run the command again.");
        }
    }
}
