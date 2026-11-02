using DndSharp.Dice;
using DndSharp.Dice.NumberGenerator;

namespace DndSharp.Discord;

public enum PresetDie { D4 = 4, D6 = 6, D8 = 8, D10 = 10, D12 = 12, D20 = 20, D100 = 100 }

public sealed class DiscordRollService
{
    public async Task<string> RollAsync(string? dice, PresetDie? preset,
        INumberGenerator? generator = null, CancellationToken token = default)
    {
        var hasText = !string.IsNullOrWhiteSpace(dice);
        if (hasText == preset.HasValue)
            throw new ArgumentException("Choose either custom dice text or a preset die, for example /roll dice:2d6+3 or /roll preset:D20.");
        if (preset.HasValue && !Enum.IsDefined(preset.Value))
            throw new ArgumentException("Choose a die from the preset list.");
        if (dice?.Length > 300)
            throw new ArgumentException("Keep dice text to 300 characters or fewer.");

        var expression = hasText ? dice! : $"d{(int)preset!.Value}";
        if (!Roll.TryParse(expression, out var roll))
            throw new ArgumentException("No dice found. Try 2d6+3, d20, or 2d6 (fire) + 1d8 (cold).");

        // Bound work, integer totals, and the size of the Discord response before rolling.
        if (roll.Count > 10 || roll.Sum(die => (long)die.Count) > 100 ||
            roll.Any(die => die.Sides > 1_000_000 || Math.Abs((long)die.Modifier) > 1_000_000))
            throw new ArgumentException("Use at most 10 dice groups, 100 dice total, 1,000,000 sides, and modifiers up to +/-1,000,000.");

        var result = await roll.Results(generator: generator, token: token);
        var lines = result.Select(item => $"{item.Dice}: [{string.Join(", ", item.Rolls)}]" +
            (item.Dice.Modifier == 0 ? "" : $" {(item.Dice.Modifier > 0 ? "+" : "-")} {Math.Abs(item.Dice.Modifier)}") +
            $" = {item.Total}");
        return $"**{roll} → {result.Total}**\n```\n{string.Join("\n", lines)}\n```";
    }
}
