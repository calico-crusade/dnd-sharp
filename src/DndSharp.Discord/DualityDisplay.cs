using System.Text.Json;
using DndSharp.Database;

namespace DndSharp.Discord;

public enum DualityOutcome
{
    SuccessFear,
    SuccessHope,
    FailureFear,
    FailureHope,
    Critical,
    Fear,
    Hope
}

public static class DualityDisplay
{
    public static string? Prefix(JsonElement entry, DualityDisplaySettings settings)
    {
        if (!settings.Enabled || entry.ValueKind != JsonValueKind.Object ||
            !entry.TryGetProperty("duality", out var duality) || duality.ValueKind != JsonValueKind.Object)
            return null;

        static int? Number(JsonElement value, string name) =>
            value.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n) ? n : null;

        var fear = Number(duality, "fear");
        var hope = Number(duality, "hope");
        if (fear is < 1 or > 12 || hope is < 1 or > 12 || fear is null || hope is null) return null;
        if (fear == hope) return settings.Critical;

        var withFear = fear > hope;
        var total = Number(entry, "total");
        if (total is null || settings.Difficulty is null) return withFear ? settings.Fear : settings.Hope;
        var success = total >= settings.Difficulty;
        return (success, withFear) switch
        {
            (true, true) => settings.SuccessFear,
            (true, false) => settings.SuccessHope,
            (false, true) => settings.FailureFear,
            _ => settings.FailureHope
        };
    }

    public static DualityDisplaySettings SetEmoji(DualityDisplaySettings settings, DualityOutcome outcome, string emoji)
    {
        if (string.IsNullOrWhiteSpace(emoji) || emoji.Length > 500 || emoji.Any(char.IsControl))
            throw new ArgumentException("The display must be 1 to 500 characters without line breaks.", nameof(emoji));
        return outcome switch
        {
            DualityOutcome.SuccessFear => settings with { SuccessFear = emoji.Trim() },
            DualityOutcome.SuccessHope => settings with { SuccessHope = emoji.Trim() },
            DualityOutcome.FailureFear => settings with { FailureFear = emoji.Trim() },
            DualityOutcome.FailureHope => settings with { FailureHope = emoji.Trim() },
            DualityOutcome.Critical => settings with { Critical = emoji.Trim() },
            DualityOutcome.Fear => settings with { Fear = emoji.Trim() },
            DualityOutcome.Hope => settings with { Hope = emoji.Trim() },
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }
}
