namespace DndSharp.PokeDex.Models.Pokemon;

using Meta;

[PkResource("pokedex/([0-9]{1,})")]
public class PkPokedex : PkLocalizedDescBase
{
    [JsonPropertyName("is_main_series")]
    public bool IsMainSeries { get; set; }

    [JsonPropertyName("pokemon_entries")]
    public Entry[] Entries { get; set; } = [];

    [JsonPropertyName("region")]
    public required PkResource Region { get; set; }

    [JsonPropertyName("version_groups")]
    public PkResource[] Versions { get; set; } = [];

    public class Entry
    {
        [JsonPropertyName("entry_number")]
        public int Number { get; set; }

        [JsonPropertyName("pokemon_species")]
        public required PkResource Species { get; set; }
    }
}
