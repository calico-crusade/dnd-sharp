namespace DndSharp.PokeDex.Models.Game;

using Meta;

[PkResource("generation/([0-9]{1,})")]
public class PkGeneration : PkLocalizedBase
{
    [JsonPropertyName("abilities")]
    public PkResource[] Abilities { get; set; } = [];

    [JsonPropertyName("main_region")]
    public required PkResource MainRegion { get; set; }

    [JsonPropertyName("moves")]
    public PkResource[] Moves { get; set; } = [];

    [JsonPropertyName("pokemon_species")]
    public PkResource[] Pokemon { get; set; } = [];

    [JsonPropertyName("version_groups")]
    public PkResource[] Versions { get; set; } = [];

    [JsonPropertyName("types")]
    public PkResource[] Types { get; set; } = [];
}
