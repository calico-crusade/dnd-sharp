using System.Collections;

namespace DndSharp.Dice;

/// <summary>
/// The result of a roll
/// </summary>
public class RollResult(DiceResult[] results) : IReadOnlyCollection<DiceResult>
{
    private Dictionary<DamageType, int>? _damageTotals;

    /// <summary>
    /// Gets all of the dice results of a specific type of damage.
    /// </summary>
    /// <param name="type">The damage type</param>
    /// <returns>The dice results</returns>
    public IEnumerable<DiceResult> this[DamageType type] => ByDamaga(type);

    /// <summary>
    /// The total result of all the dice rolls in this result
    /// </summary>
    public int Total => results.Sum(r => r.Total);

    /// <summary>
    /// The total amount of damage done by type
    /// </summary>
    /// <remarks>This is lazily computed</remarks>
    public Dictionary<DamageType, int> TotalsByDamage => _damageTotals ??= results
        .GroupBy(r => r.Dice.Type)
        .ToDictionary(
            g => g.Key,
            g => g.Sum(r => r.Total));

    /// <inheritdoc />
    public int Count => results.Length;

    /// <inheritdoc />
    public IEnumerator<DiceResult> GetEnumerator() => results.AsEnumerable().GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Gets all of the dice results of a specific type of damage.
    /// </summary>
    /// <param name="type">The damage type</param>
    /// <returns>The dice results</returns>
    public IEnumerable<DiceResult> ByDamaga(DamageType type)
    {
        return results.Where(t => t.Dice.Type == type);
    }

    /// <summary>
    /// The total amount of damage done by the given damage type
    /// </summary>
    /// <param name="type">The damage type</param>
    /// <returns>The total amount rolled for the given damage type</returns>
    public int TotalByDamage(DamageType type)
    {
        return TotalsByDamage.TryGetValue(type, out var total) ? total : 0;
    }
}