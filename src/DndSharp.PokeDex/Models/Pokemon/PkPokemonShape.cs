namespace DndSharp.PokeDex.Models.Pokemon;

using Meta;

[PkResource("pokemon-shape/([0-9]{1,})")]
public class PkPokemonShape : PkLocalizedBase
{
    [JsonPropertyName("awesome_names")]
    public AwesomeName[] AwesomeNames { get; set; } = [];

    [JsonPropertyName("pokemon_species")]
    public PkResource[] Species { get; set; } = [];

    public class AwesomeName : IPkResource
    {
        [JsonPropertyName("awesome_name")]
        public required string Name { get; set; }

        [JsonPropertyName("language")]
        public required PkResource Resource { get; set; }
    }
}
