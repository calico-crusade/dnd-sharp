using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DndSharp.HeartOfDaggers;

public sealed record CampaignHistoryItem(long CampaignId, string Id, JsonElement Entry);

/// <summary>Suppresses previously observed history entries, including duplicate socket deliveries.</summary>
public sealed class CampaignHistoryTracker(long campaignId)
{
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    public long CampaignId => campaignId;
    public bool HasBaseline { get; private set; }

    public void Seed(IEnumerable<JsonElement> history)
    {
        foreach (var item in history) TryObserve(item, out _);
        HasBaseline = true;
    }

    public bool TryObserve(JsonElement entry, out CampaignHistoryItem? item)
    {
        item = null;
        if (entry.ValueKind != JsonValueKind.Object) return false;
        var kind = String(entry, "type") ?? String(entry, "rollKind") ?? "";
        var action = String(entry, "action") ?? String(entry, "spotlightAction") ?? "";
        if (kind.Equals("inventory_sync", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("spotlight_cleared", StringComparison.OrdinalIgnoreCase) ||
            (kind.Equals("spotlight", StringComparison.OrdinalIgnoreCase) && action.Equals("cleared", StringComparison.OrdinalIgnoreCase)))
            return false;
        var id = String(entry, "id");
        if (string.IsNullOrWhiteSpace(id))
            id = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.GetRawText())));
        if (!seen.Add(id)) return false;
        item = new CampaignHistoryItem(campaignId, id, entry.Clone());
        return true;
    }

    private static string? String(JsonElement item, string key) =>
        item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
