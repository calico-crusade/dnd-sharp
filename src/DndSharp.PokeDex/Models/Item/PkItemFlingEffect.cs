namespace DndSharp.PokeDex.Models.Item;

using Meta;

[PkResource("item-fling-effect/([0-9]{1,})")]
public class PkItemFlingEffect : PkBase
{
    [JsonPropertyName("effect_entries")]
    public EffectEntry[] EffectEntries { get; set; } = [];

    [JsonPropertyName("items")]
    public PkResource[] Items { get; set; } = [];

    public class EffectEntry : IPkResource
    {
        [JsonPropertyName("effect")]
        public required string Value { get; set; }

        [JsonPropertyName("language")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("short_effect")]
        public string? ShortEffect { get; set; }
    }
}
