namespace DndSharp.PokeDex.Models.Game;

using Meta;

[PkResource("version-group/([0-9]{1,})")]
public class PkVersionGroup : PkBase
{
    [JsonPropertyName("generation")]
    public required PkResource Generation { get; set; }

    [JsonPropertyName("move_learn_methods")]
    public PkResource[] MoveMethods { get; set; } = [];

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("pokedexes")]
    public PkResource[] Pokedexes { get; set; } = [];

    [JsonPropertyName("regions")]
    public PkResource[] Regions { get; set; } = [];

    [JsonPropertyName("versions")]
    public PkResource[] Versions { get; set; } = [];
}
