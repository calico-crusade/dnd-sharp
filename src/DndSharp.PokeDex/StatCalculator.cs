using DndSharp.PokeDex.Models.Pokemon;

namespace DndSharp.PokeDex;

public class StatCalculator
{
    /// <summary>
    /// The base stats for the pokemon
    /// </summary>
    public required IPokemonStats Stats { get; set; }

    /// <summary>
    /// The optional IVs for the pokemon
    /// </summary>
    public IPokemonStats? IVs { get; set; }

    /// <summary>
    /// The optional EVs for the pokemon
    /// </summary>
    public IPokemonStats? EVs { get; set; }

    /// <summary>
    /// The nature modifiers for the pokemon
    /// </summary>
    /// <remarks>HP is ignored here</remarks>
    public IPokemonStats? NatureModifiers { get; set; }

    internal static int Stat(int @base, int level, int iv, int ev, int nature)
    {
        var a = ((2 * @base) + iv + Math.Floor(ev / 4d)) * level;
        var b = Math.Floor(a / 100) + 5;
        var n = nature == 0 ? 1 : nature < 0 ? 0.9 : 1.1;
        return (int)Math.Floor(b * n);
    }

    internal static int Hp(int @base, int level, int iv, int ev)
    {
        var a = ((2 * @base) + iv + Math.Floor(ev / 4d)) * level;
        var b = Math.Floor(a / 100) + level + 10;
        return (int)b;
    }

    /// <summary>
    /// Calculate the final stats for the pokemon at the given level
    /// </summary>
    /// <param name="level">The level of the pokemon</param>
    /// <param name="errors">Any errors that occurred</param>
    /// <returns>The calculated stats</returns>
    public PokemonStats Calculate(int level, out string[] errors)
    {
        List<string> err = [];
        Stats.Validate(err);
        IVs?.Validate(err);
        EVs?.Validate(err);
        NatureModifiers?.Validate(err);

        if (err.Count > 0)
        {
            errors = [.. err];
            return PokemonStats.Empty;
        }

        var ivs = IVs ?? PokemonStats.Empty;
        var evs = EVs ?? PokemonStats.Empty;
        var natures = NatureModifiers ?? PokemonStats.Empty;

        errors = [];
        return new PokemonStats(
            Hp(Stats.Hp, level, ivs.Hp, evs.Hp),
            Stat(Stats.Attack, level, ivs.Attack, evs.Attack, natures.Attack),
            Stat(Stats.Defense, level, ivs.Defense, evs.Defense, natures.Defense),
            Stat(Stats.SpecialAttack, level, ivs.SpecialAttack, evs.SpecialAttack, natures.SpecialAttack),
            Stat(Stats.SpecialDefense, level, ivs.SpecialDefense, evs.SpecialDefense, natures.SpecialDefense),
            Stat(Stats.Speed, level, ivs.Speed, evs.Speed, natures.Speed));
    }

    /// <summary>
    /// Convert the given nature to a valid <see cref="NatureModifiers"/> value
    /// </summary>
    /// <param name="nature">The nature to use</param>
    /// <returns>The stats</returns>
    public static IPokemonStats Nature(PkNature nature)
    {
        int attack = 0, defense = 0, specialAttack = 0, specialDefense = 0, speed = 0;

        Dictionary<string, Action<int>> settings = new(StringComparer.InvariantCultureIgnoreCase)
        {
            ["attack"] = (i) => attack = i,
            ["defense"] = (i) => defense = i,
            ["special-attack"] = (i) => specialAttack = i,
            ["special-defense"] = (i) => specialDefense = i,
            ["speed"] = (i) => speed = i
        };

        if (nature.IncreasedStat is not null && 
            !string.IsNullOrEmpty(nature.IncreasedStat.Name) &&
            settings.TryGetValue(nature.IncreasedStat.Name, out var action))
            action(1);

        if (nature.DecreasedStat is not null &&
            !string.IsNullOrEmpty(nature.DecreasedStat.Name) &&
            settings.TryGetValue(nature.DecreasedStat.Name, out action))
            action(-1);

        return new PokemonStats(
            0,
            attack,
            defense,
            specialAttack,
            specialDefense,
            speed);
    }
}
