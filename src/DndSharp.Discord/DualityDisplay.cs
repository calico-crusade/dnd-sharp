using System.Text.Json;
using System.Text.RegularExpressions;
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
    private static readonly Regex UnlabeledDice = new(
        @"\[(?<leading>\s*)(?<first>\d+)(?<separator>\s*,\s*)(?<second>\d+)(?=\s*[,\]])",
        RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex TraitNotation = new(
        @"^(?<total>-?\d+)\s+(?:with\s+(?:Fear|Hope)|critical(?:\s+success)?)\s+(?<breakdown>\[.*\])$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex LabeledDie = new(
        @"^\s*(?<label>Fear|Hope)\s+(?<value>\d+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex Modifier = new(
        @"^\s*(?<sign>[+-])\s*(?<value>\d+)(?:\s+.*)?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    internal static string FormatNotation(JsonElement entry, string notation)
    {
        notation = LabelDice(entry, notation);
        var match = TraitNotation.Match(notation);
        if (!match.Success || entry.ValueKind != JsonValueKind.Object || !long.TryParse(match.Groups["total"].Value, out var total) ||
            !entry.TryGetProperty("duality", out var duality) || duality.ValueKind != JsonValueKind.Object ||
            !duality.TryGetProperty("fear", out var fearValue) || fearValue.ValueKind != JsonValueKind.Number ||
            !fearValue.TryGetInt32(out var fear) || fear is < 1 or > 12 ||
            !duality.TryGetProperty("hope", out var hopeValue) || hopeValue.ValueKind != JsonValueKind.Number ||
            !hopeValue.TryGetInt32(out var hope) || hope is < 1 or > 12)
            return notation;
        if (entry.TryGetProperty("total", out var entryTotal) &&
            (entryTotal.ValueKind != JsonValueKind.Number || !entryTotal.TryGetInt64(out var reportedTotal) || reportedTotal != total))
            return notation;

        var breakdown = match.Groups["breakdown"].Value;
        var parts = breakdown[1..^1].Split(',');
        if (parts.Length < 2)
            return notation;
        var first = LabeledDie.Match(parts[0]);
        var second = LabeledDie.Match(parts[1]);
        if (!first.Success || !second.Success || first.Groups["label"].Value.Equals(second.Groups["label"].Value, StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(first.Groups["value"].Value, out var firstValue) || !int.TryParse(second.Groups["value"].Value, out var secondValue) ||
            firstValue != (first.Groups["label"].Value.Equals("Fear", StringComparison.OrdinalIgnoreCase) ? fear : hope) ||
            secondValue != (second.Groups["label"].Value.Equals("Fear", StringComparison.OrdinalIgnoreCase) ? fear : hope))
            return notation;

        long modifiers = 0;
        try
        {
            foreach (var part in parts.Skip(2))
            {
                var modifier = Modifier.Match(part);
                if (!modifier.Success || !long.TryParse(modifier.Groups["value"].Value, out var amount))
                    return notation;
                modifiers = checked(modifiers + (modifier.Groups["sign"].Value == "-" ? -amount : amount));
            }
            if (checked(fear + (long)hope + modifiers) != total)
                return notation;
        }
        catch (OverflowException)
        {
            return notation;
        }
        var suffix = modifiers > 0 ? $"+{modifiers}" : modifiers < 0 ? modifiers.ToString() : "";
        return $"2d12{suffix} = {total} {breakdown}";
    }

    internal static string LabelDice(JsonElement entry, string notation)
    {
        if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("duality", out var duality) ||
            duality.ValueKind != JsonValueKind.Object || !duality.TryGetProperty("fear", out var fearValue) ||
            fearValue.ValueKind != JsonValueKind.Number || !fearValue.TryGetInt32(out var fear) || fear is < 1 or > 12 ||
            !duality.TryGetProperty("hope", out var hopeValue) || hopeValue.ValueKind != JsonValueKind.Number ||
            !hopeValue.TryGetInt32(out var hope) || hope is < 1 or > 12)
            return notation;

        return UnlabeledDice.Replace(notation, match =>
        {
            if (!int.TryParse(match.Groups["first"].Value, out var first) ||
                !int.TryParse(match.Groups["second"].Value, out var second))
                return match.Value;
            string firstLabel, secondLabel;
            if (first == fear && second == hope)
                (firstLabel, secondLabel) = ("Fear", "Hope");
            else if (first == hope && second == fear)
                (firstLabel, secondLabel) = ("Hope", "Fear");
            else
                return match.Value;
            return $"[{match.Groups["leading"].Value}{firstLabel} {match.Groups["first"].Value}{match.Groups["separator"].Value}{secondLabel} {match.Groups["second"].Value}";
        }, 1);
    }

    public static string? Prefix(JsonElement entry, DualityDisplaySettings settings)
    {
        if (!settings.Enabled || entry.ValueKind != JsonValueKind.Object ||
            !entry.TryGetProperty("duality", out var duality) || duality.ValueKind != JsonValueKind.Object)
            return null;

        static int? Number(JsonElement value, string name) =>
            value.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n) ? n : null;

        var fear = Number(duality, "fear");
        var hope = Number(duality, "hope");
        if (fear is < 1 or > 12 || hope is < 1 or > 12 || fear is null || hope is null)
            return null;
        if (fear == hope)
            return settings.Critical;

        var withFear = fear > hope;
        var total = Number(entry, "total");
        if (total is null || settings.Difficulty is null)
            return withFear ? settings.Fear : settings.Hope;
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
