namespace DndSharp.PokeDex.Models.Game;

using Meta;

[PkResource("location-area/([0-9]{1,})")]
public class PkLocationArea : PkLocalizedBase
{
    [JsonPropertyName("encounter_method_rates")]
    public Method[] EncounterRates { get; set; } = [];

    [JsonPropertyName("game_index")]
    public int GameIndex { get; set; }

    [JsonPropertyName("location")]
    public required PkResource Location { get; set; }

    [JsonPropertyName("pokemon_encounters")]
    public Encounter[] EncounterPokemon { get; set; } = [];

    public class Encounter : IPkResource
    {
        [JsonPropertyName("pokemon")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("version_details")]
        public EncounterDetail[] Details { get; set; } = [];
    }

    public class Method : IPkResource
    {
        [JsonPropertyName("encounter_method")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("version_details")]
        public Detail[] Versions { get; set; } = [];
    }

    public class Detail : IPkResource
    {
        [JsonPropertyName("version")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("rate")]
        public int Rate { get; set; }
    }

    public class EncounterDetail : IPkResource
    {
        [JsonPropertyName("max_chance")]
        public int MaxChance { get; set; }

        [JsonPropertyName("version")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("encounter_details")]
        public EncounterData[] Details { get; set; } = [];
    }

    public class EncounterData : IPkResource
    {
        [JsonPropertyName("chance")]
        public int Chance { get; set; }

        [JsonPropertyName("max_level")]
        public int MaxLevel { get; set; }

        [JsonPropertyName("min_level")]
        public int MinLevel { get; set; }

        [JsonPropertyName("condition_values")]
        public PkResource[] ConditionValues { get; set; } = [];

        [JsonPropertyName("method")]
        public required PkResource Resource { get; set; }

    }
}
