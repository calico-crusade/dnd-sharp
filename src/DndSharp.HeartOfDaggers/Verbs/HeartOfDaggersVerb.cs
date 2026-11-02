using System.Text.Json;
using CardboardBox;
using CommandLine;
using Microsoft.Extensions.Logging;

namespace DndSharp.HeartOfDaggers.Verbs;

[Verb("heart-of-daggers", HelpText = "Inspect a campaign and watch new history entries.")]
public sealed class HeartOfDaggersOptions
{
    [Option('c', "campaign", HelpText = "Campaign URL or slug.")]
    public string Campaign { get; set; } = "";

    [Option("invite", HelpText = "Accept an invitation without linking a character, then inspect the campaign.")]
    public string? Invite { get; set; }

    [Option("snapshot-only", HelpText = "Print campaign and character data, then exit.")]
    public bool SnapshotOnly { get; set; }

    [Option("show-history", HelpText = "Include existing history in the initial snapshot.")]
    public bool ShowHistory { get; set; }
}

public sealed class HeartOfDaggersVerb(
    ILogger<HeartOfDaggersVerb> logger,
    HeartOfDaggersClient api,
    CampaignInvitationService invitations,
    CampaignHistoryWatcher watcher) : BooleanVerb<HeartOfDaggersOptions>(logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public override async Task<bool> Execute(HeartOfDaggersOptions options, CancellationToken token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(options.Campaign) && string.IsNullOrWhiteSpace(options.Invite))
                throw new ArgumentException("Specify --campaign or --invite.");
            if (!string.IsNullOrWhiteSpace(options.Campaign) && !string.IsNullOrWhiteSpace(options.Invite))
                throw new ArgumentException("Specify either --campaign or --invite, not both.");
            var campaign = !string.IsNullOrWhiteSpace(options.Invite)
                ? await invitations.JoinWithoutCharacterAsync(options.Invite, token)
                : await api.GetCampaignAsync(options.Campaign, token);
            var characters = await api.GetCampaignCharacterDetailsAsync(campaign, token);
            var history = options.ShowHistory ? await api.GetCampaignHistoryAsync(campaign, token) : null;
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                campaign = new { campaign.Id, campaign.Slug, campaign.AccessMode },
                characters,
                history
            }, JsonOptions));
            if (options.SnapshotOnly) return true;

            logger.LogInformation("Watching for new history entries. Press Ctrl+C to stop.");
            await watcher.WatchAsync(campaign.Slug, (item, _) =>
            {
                Console.WriteLine(JsonSerializer.Serialize(new { campaignId = item.CampaignId, entry = item.Entry }, JsonOptions));
                return Task.CompletedTask;
            }, token);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return true;
        }
    }
}
