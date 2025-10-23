namespace DndSharp.PokeDex.Models.Encounter;

using Meta;

[PkResource("pokemon/([0-9]{1,})/encounters")]
public class PkEncounter
{
    [JsonPropertyName("location_area")]
    public required PkResource Location { get; set; }

    [JsonPropertyName("version_details")]
    public Version[] Versions { get; set; } = [];

    public class Version : IPkResource
    {
        [JsonPropertyName("version")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("max_chance")]
        public int MaxChance { get; set; }

        [JsonPropertyName("encounter_details")]
        public Encounter[] Encounters { get; set; } = [];
    }

    public class Encounter
    {
        [JsonPropertyName("chance")]
        public int Chance { get; set; }

        [JsonPropertyName("max_level")]
        public int MaxLevel { get; set; }

        [JsonPropertyName("min_level")]
        public int MinLevel { get; set; }

        [JsonPropertyName("method")]
        public required PkResource Method { get; set; }

        [JsonPropertyName("condition_values")]
        public PkResource[] Conditions { get; set; } = [];
    }
}
