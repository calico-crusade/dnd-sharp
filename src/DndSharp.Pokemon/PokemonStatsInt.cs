namespace DndSharp.Pokemon;

internal class PokemonStatsInt : IPokemonStats
{
    public int Hp { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int SpecialAttack { get; set; }
    public int SpecialDefense { get; set; }
    public int Speed { get; set; }
    public void Validate(List<string> errors) { }
    public (bool, string[] errors) Validate()
    {
        List<string> errors = [];
        Validate(errors);
        return (errors.Count == 0, [.. errors]);
    }

    public static implicit operator PokemonStats(PokemonStatsInt stats)
    {
        return new PokemonStats(
            stats.Hp,
            stats.Attack,
            stats.Defense,
            stats.SpecialAttack,
            stats.SpecialDefense,
            stats.Speed);
    }

    public static implicit operator PokemonIVs(PokemonStatsInt stats)
    {
        return new PokemonIVs(
            stats.Hp,
            stats.Attack,
            stats.Defense,
            stats.SpecialAttack,
            stats.SpecialDefense,
            stats.Speed);
    }

    public static implicit operator PokemonEVs(PokemonStatsInt stats)
    {
        return new PokemonEVs(
            stats.Hp,
            stats.Attack,
            stats.Defense,
            stats.SpecialAttack,
            stats.SpecialDefense,
            stats.Speed);
    }
}
