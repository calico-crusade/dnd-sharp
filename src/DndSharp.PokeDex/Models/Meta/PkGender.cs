namespace DndSharp.PokeDex.Models.Meta;

[PkResource("gender/([0-9]{1,})")]
public class PkGender : PkBase
{
    [JsonPropertyName("pokemon_species_details")]
    public Spec[] Species { get; set; } = [];

    [JsonPropertyName("required_for_evolution")]
    public PkResource[] RequiredForEvolution { get; set; } = [];

    public class Spec : IPkResource
    {
        [JsonPropertyName("pokemon_species")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("rate")]
        public int Rate { get; set; }
    }
}

