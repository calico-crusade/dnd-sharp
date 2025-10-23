namespace DndSharp.PokeDex.Models.Evolution;

using Meta;

[PkResource("evolution-trigger/([0-9]{1,})")]
public class PkEvolutionTrigger : PkBase
{
    [JsonPropertyName("names")]
    public PkLocalization[] Names { get; set; } = [];

    [JsonPropertyName("pokemon_species")]
    public PkResource[] Species { get; set; } = [];
}
