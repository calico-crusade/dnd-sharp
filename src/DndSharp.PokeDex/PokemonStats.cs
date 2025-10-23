namespace DndSharp.PokeDex;

public record class PokemonStats(
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("atk")] int Attack,
    [property: JsonPropertyName("def")] int Defense,
    [property: JsonPropertyName("spa")] int SpecialAttack,
    [property: JsonPropertyName("spd")] int SpecialDefense,
    [property: JsonPropertyName("spe")] int Speed) : IPokemonStats
{
    public virtual void Validate(List<string> errors) { }

    public virtual (bool, string[] errors) Validate()
    {
        List<string> errors = [];
        Validate(errors);
        return (errors.Count == 0, [.. errors]);
    }

    public static readonly PokemonStats Empty = new(0, 0, 0, 0, 0, 0);
}