namespace DndSharp.PokeDex.Models.Game;

using Meta;

[PkResource("region/([0-9]{1,})")]
public class PkRegion : PkLocalizedBase
{
    [JsonPropertyName("locations")]
    public PkResource[] Locations { get; set; } = [];

    [JsonPropertyName("main_generation")]
    public required PkResource MainGeneration { get; set; }

    [JsonPropertyName("pokedexes")]
    public PkResource[] Pokedexes { get; set; } = [];

    [JsonPropertyName("version_groups")]
    public PkResource[] Versions { get; set; } = [];
}
