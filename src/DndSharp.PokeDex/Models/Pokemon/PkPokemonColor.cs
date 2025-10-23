namespace DndSharp.PokeDex.Models.Pokemon;

using Meta;

[PkResource("pokemon-color/([0-9]{1,})")]
public class PkPokemonColor : PkLocalizedBase
{
    [JsonPropertyName("pokemon_species")]
    public PkResource[] Species { get; set; } = [];
}
