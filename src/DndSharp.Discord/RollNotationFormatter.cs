using System.Text.Json;
using System.Text.RegularExpressions;

namespace DndSharp.Discord;

internal static class RollNotationFormatter
{
    private static readonly Regex ResultOnly = new(@"^(?<total>-?\d+)\s+(?<breakdown>\[.*\])$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex DiceExpression = new(@"\b\d*[dD]\d+\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public static string Format(JsonElement entry, string notation, string baseNotation, string label)
    {
        notation = DualityDisplay.FormatNotation(entry, notation);
        var match = ResultOnly.Match(notation);
        if (!match.Success || entry.ValueKind != JsonValueKind.Object ||
            !long.TryParse(match.Groups["total"].Value, out var total))
            return notation;
        var kind = entry.TryGetProperty("rollKind", out var rollKind) && rollKind.ValueKind == JsonValueKind.String
            ? rollKind.GetString() : null;
        if (!string.Equals(label, "Custom Roll", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(kind, "quick", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(kind, "custom", StringComparison.OrdinalIgnoreCase))
            return notation;
        if (entry.TryGetProperty("total", out var reported) &&
            (reported.ValueKind != JsonValueKind.Number || !reported.TryGetInt64(out var reportedTotal) || reportedTotal != total))
            return notation;

        var expression = DiceExpression.IsMatch(baseNotation) ? baseNotation : FromGroups(entry);
        if (string.IsNullOrWhiteSpace(expression))
            return notation;
        return $"{expression} = {total} {match.Groups["breakdown"].Value}";
    }

    private static string? FromGroups(JsonElement entry)
    {
        if (!entry.TryGetProperty("groups", out var groups) || groups.ValueKind != JsonValueKind.Array ||
            groups.GetArrayLength() is < 1 or > 50)
            return null;
        var dice = new List<string>();
        foreach (var group in groups.EnumerateArray())
        {
            if (group.ValueKind != JsonValueKind.Object || !group.TryGetProperty("qty", out var quantityValue) ||
                quantityValue.ValueKind != JsonValueKind.Number || !quantityValue.TryGetInt32(out var quantity) || quantity is < 1 or > 10000 ||
                !group.TryGetProperty("sides", out var sidesValue) || sidesValue.ValueKind != JsonValueKind.Number ||
                !sidesValue.TryGetInt32(out var sides) || sides is < 2 or > 1000000)
                return null;
            dice.Add($"{(quantity == 1 ? "" : quantity.ToString())}D{sides}");
        }
        var expression = string.Join("+", dice);
        if (entry.TryGetProperty("modifier", out var modifier))
        {
            if (modifier.ValueKind != JsonValueKind.Number || !modifier.TryGetInt32(out var value))
                return null;
            if (value > 0) expression += $"+{value}";
            else if (value < 0) expression += value.ToString();
        }
        return expression;
    }
}
