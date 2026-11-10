using System.Text.Json;
using System.Text.RegularExpressions;

namespace DndSharp.Discord;

internal static class ReactionRollFormatter
{
    private static readonly Regex Result = new(@"^(?<total>-?\d+)\s+\[(?<breakdown>.*)\]$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex Modifier = new(@"^\s*(?<sign>[+-])\s*(?<value>\d+)(?:\s+.*)?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public static bool IsReaction(JsonElement entry) => entry.ValueKind == JsonValueKind.Object &&
        ((entry.TryGetProperty("reactionRoll", out var flag) && flag.ValueKind == JsonValueKind.True) ||
         (entry.TryGetProperty("rollKind", out var kind) && kind.ValueKind == JsonValueKind.String &&
          string.Equals(kind.GetString(), "reaction", StringComparison.OrdinalIgnoreCase)));

    public static string Format(JsonElement entry, string notation)
    {
        var result = Result.Match(notation);
        if (!result.Success || !long.TryParse(result.Groups["total"].Value, out var total) ||
            !entry.TryGetProperty("total", out var totalValue) || totalValue.ValueKind != JsonValueKind.Number ||
            !totalValue.TryGetInt64(out var reportedTotal) || reportedTotal != total ||
            !entry.TryGetProperty("rolls", out var rolls) || rolls.ValueKind != JsonValueKind.Array ||
            !entry.TryGetProperty("groups", out var groups) || groups.ValueKind != JsonValueKind.Array ||
            !entry.TryGetProperty("advMode", out var modeValue) || modeValue.ValueKind != JsonValueKind.String)
            return notation;

        var mode = modeValue.GetString()?.ToLowerInvariant();
        var dieSign = mode switch { "adv" or "advantage" => 1, "dis" or "disadvantage" => -1, "normal" => 0, _ => 2 };
        if (dieSign == 2 || rolls.GetArrayLength() != (dieSign == 0 ? 2 : 3))
            return notation;
        var sides = new List<int>();
        foreach (var group in groups.EnumerateArray())
        {
            if (group.ValueKind != JsonValueKind.Object || !group.TryGetProperty("sides", out var face) ||
                face.ValueKind != JsonValueKind.Number || !face.TryGetInt32(out var faceCount) ||
                !group.TryGetProperty("qty", out var qty) || qty.ValueKind != JsonValueKind.Number ||
                !qty.TryGetInt32(out var count) || count is < 1 or > 2 || sides.Count + count > 3)
                return notation;
            sides.AddRange(Enumerable.Repeat(faceCount, count));
        }
        if (!sides.SequenceEqual(dieSign == 0 ? new[] { 12, 12 } : new[] { 12, 12, 6 }))
            return notation;
        if (!ReadDie(rolls[0], 12, out var fear) || !ReadDie(rolls[1], 12, out var hope))
            return notation;
        var parts = result.Groups["breakdown"].Value.Split(',').Select(part => part.Trim()).ToArray();
        if (parts.Length < 2 || !int.TryParse(parts[0], out var first) || first != fear ||
            !int.TryParse(parts[1], out var second) || second != hope)
            return notation;

        var advantage = 0;
        if (dieSign != 0)
        {
            if (parts.Length < 3 || !ReadDie(rolls[2], 6, out advantage) ||
                !entry.TryGetProperty("advDie", out var advValue) || advValue.ValueKind != JsonValueKind.Number ||
                !advValue.TryGetInt32(out var reportedAdvantage) || Math.Abs((long)reportedAdvantage) != advantage)
                return notation;
            var bonusDie = Modifier.Match(parts[2]);
            if (!bonusDie.Success || !int.TryParse(bonusDie.Groups["value"].Value, out var amount) || amount != advantage ||
                (bonusDie.Groups["sign"].Value == "+" ? 1 : -1) != dieSign)
                return notation;
        }

        long fixedModifiers = 0;
        try
        {
            foreach (var part in parts.Skip(dieSign == 0 ? 2 : 3))
            {
                var modifier = Modifier.Match(part);
                if (!modifier.Success || !long.TryParse(modifier.Groups["value"].Value, out var amount))
                    return notation;
                fixedModifiers = checked(fixedModifiers + (modifier.Groups["sign"].Value == "+" ? amount : -amount));
            }
            if (checked((long)fear + hope + dieSign * advantage + fixedModifiers) != total)
                return notation;
        }
        catch (OverflowException)
        {
            return notation;
        }

        var expression = "2d12" + (dieSign == 1 ? "+1d6" : dieSign == -1 ? "-1d6" : "");
        if (fixedModifiers > 0) expression += $"+{fixedModifiers}";
        else if (fixedModifiers < 0) expression += fixedModifiers.ToString();
        parts[0] = $"Fear {fear}";
        parts[1] = $"Hope {hope}";
        if (dieSign != 0)
            parts[2] = $"{(dieSign == 1 ? "+" : "-")}{advantage} {(dieSign == 1 ? "Advantage" : "Disadvantage")}";
        return $"{expression} = {total} [{string.Join(", ", parts)}]";
    }

    private static bool ReadDie(JsonElement value, int sides, out int result)
    {
        result = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result) && result >= 1 && result <= sides;
    }
}
