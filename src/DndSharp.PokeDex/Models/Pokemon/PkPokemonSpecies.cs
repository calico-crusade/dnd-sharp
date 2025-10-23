namespace DndSharp.PokeDex.Models.Pokemon;

using Meta;

[PkResource("pokemon-species/([0-9]{1,})")]
public class PkPokemonSpecies : PkLocalizedBase
{
    [JsonPropertyName("base_happiness")]
    public int BaseHappiness { get; set; }

    [JsonPropertyName("capture_rate")]
    public int CaptureRate { get; set; }

    [JsonPropertyName("color")]
    public required PkResource Color { get; set; }

    [JsonPropertyName("egg_groups")]
    public PkResource[] EggGroups { get; set; } = [];

    [JsonPropertyName("evolution_chain")]
    public PkResource? EvolutionChain { get; set; }

    [JsonPropertyName("evolves_from_species")]
    public PkResource? EvolvesFrom { get; set; }

    [JsonPropertyName("flavor_text_entries")]
    public FlavorText[] FlavorTexts { get; set; } = [];

    [JsonPropertyName("form_descriptions")]
    public Form[] FormDescriptions { get; set; } = [];

    [JsonPropertyName("forms_switchable")]
    public bool FormsSwitchable { get; set; }

    [JsonPropertyName("gender_rate")]
    public int GenderRate { get; set; }

    [JsonPropertyName("genera")]
    public Genus[] Genera { get; set; } = [];

    [JsonPropertyName("generation")]
    public required PkResource Generation { get; set; }

    [JsonPropertyName("growth_rate")]
    public required PkResource GrowthRate { get; set; }

    [JsonPropertyName("habitat")]
    public required PkResource Habitat { get; set; }

    [JsonPropertyName("has_gender_differences")]
    public bool HasGenderDifferences { get; set; }

    [JsonPropertyName("hatch_counter")]
    public int HatchCounter { get; set; }

    [JsonPropertyName("is_baby")]
    public bool IsBaby { get; set; }

    [JsonPropertyName("is_legendary")]
    public bool IsLegendary { get; set; }

    [JsonPropertyName("is_mythical")]
    public bool IsMythical { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("pokedex_numbers")]
    public PokedexEntry[] PokedexEntries { get; set; } = [];

    [JsonPropertyName("shape")]
    public required PkResource Shape { get; set; }

    [JsonPropertyName("varieties")]
    public Variety[] Varieties { get; set; } = [];

    public class Form : IPkResource
    {
        [JsonPropertyName("description")]
        public required string Description { get; set; }

        [JsonPropertyName("language")]
        public required PkResource Resource { get; set; }
    }

    public class FlavorText : IPkResource
    {
        [JsonPropertyName("flavor_text")]
        public required string Value { get; set; }

        [JsonPropertyName("language")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("version")]
        public required PkResource Version { get; set; }
    }

    public class PokedexEntry : IPkResource
    {
        [JsonPropertyName("entry_number")]
        public int EntryNumber { get; set; }

        [JsonPropertyName("pokedex")]
        public required PkResource Resource { get; set; }
    }

    public class Variety : IPkResource
    {
        [JsonPropertyName("is_default")]
        public bool IsDefault { get; set; }

        [JsonPropertyName("pokemon")]
        public required PkResource Resource { get; set; }
    }

    public class Genus : IPkResource
    {
        [JsonPropertyName("genus")]
        public required string Value { get; set; }

        [JsonPropertyName("language")]
        public required PkResource Resource { get; set; }
    }
}
