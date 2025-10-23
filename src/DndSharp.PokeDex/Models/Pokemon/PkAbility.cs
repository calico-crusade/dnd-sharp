namespace DndSharp.PokeDex.Models.Pokemon;

using Meta;

[PkResource("ability/([0-9]{1,})")]
public class PkAbility : PkLocalizedBase
{
    [JsonPropertyName("is_main_series")]
    public bool IsMainSeries { get; set; }

    [JsonPropertyName("effect_changes")]
    public Effect[] Effects { get; set; } = [];

    [JsonPropertyName("effect_entries")]
    public Entry[] EffectEntries { get; set; } = [];

    [JsonPropertyName("flavor_text_entries")]
    public FlavorText[] FlavorTexts { get; set; } = [];

    [JsonPropertyName("generation")]
    public required PkResource Generation { get; set; }

    [JsonPropertyName("pokemon")]
    public PokemonRef[] Pokemon { get; set; } = [];

    public class PokemonRef : IPkResource
    {
        [JsonPropertyName("is_hidden")]
        public bool IsHidden { get; set; }

        [JsonPropertyName("pokemon")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("slot")]
        public int Slot { get; set; }
    }

    public class Effect : IPkResource
    {
        [JsonPropertyName("effect_entries")]
        public Entry[] Entries { get; set; } = [];

        [JsonPropertyName("version_group")]
        public required PkResource Resource { get; set; }
    }

    public class Entry
    {
        [JsonPropertyName("effect")]
        public required string Effect { get; set; }

        [JsonPropertyName("language")]
        public required PkResource Language { get; set; }

        [JsonPropertyName("short_effect")]
        public string? ShortEffect { get; set; }
    }

    public class FlavorText
    {
        [JsonPropertyName("flavor_text")]
        public required string Text { get; set; }

        [JsonPropertyName("language")]
        public required PkResource Language { get; set; }

        [JsonPropertyName("version_group")]
        public required PkResource VersionGroup { get; set; }
    }
}