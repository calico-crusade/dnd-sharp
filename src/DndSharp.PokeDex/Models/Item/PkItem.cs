namespace DndSharp.PokeDex.Models.Item;

using Meta;

[PkResource("item/([0-9]{1,})")]
public class PkItem : PkLocalizedBase
{
    [JsonPropertyName("attributes")]
    public PkResource[] Attributes { get; set; } = [];

    [JsonPropertyName("baby_trigger_for")]
    public PkResource? BabyTriggerFor { get; set; }

    [JsonPropertyName("category")]
    public PkResource? Category { get; set; }

    [JsonPropertyName("prices")]
    public Price[] Prices { get; set; } = [];

    [JsonPropertyName("effect_entries")]
    public EffectEntry[] Effects { get; set; } = [];

    [JsonPropertyName("flavor_text_entries")]
    public FlavorText[] FlavorTexts { get; set; } = [];

    [JsonPropertyName("fling_power")]
    public int? FlingPower { get; set; }

    [JsonPropertyName("fling_effect")]
    public PkResource? FlingEffect { get; set; }

    [JsonPropertyName("held_by_pokemon")]
    public HeldPokemon[] HeldByPokemon { get; set; } = [];

    [JsonPropertyName("machines")]
    public Machine[] Machines { get; set; } = [];

    [JsonPropertyName("sprites")]
    public required SpritesVals Sprites { get; set; }

    [JsonPropertyName("game_indices")]
    public GameIndex[] GameIndices { get; set; } = [];

    public class EffectEntry : IPkResource
    {
        [JsonPropertyName("effect")]
        public required string Value { get; set; }

        [JsonPropertyName("language")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("short_effect")]
        public string? ShortEffect { get; set; }
    }

    public class FlavorText : IPkResource
    {
        [JsonPropertyName("text")]
        public required string Value { get; set; }

        [JsonPropertyName("language")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("version_group")]
        public required PkResource Version { get; set; }
    }

    public class SpritesVals
    {
        [JsonPropertyName("default")]
        public required string Default { get; set; }
    }

    public class GameIndex : IPkResource
    {
        [JsonPropertyName("game_index")]
        public int Index { get; set; }

        [JsonPropertyName("generation")]
        public required PkResource Resource { get; set; }
    }

    public class Price
    {
        [JsonPropertyName("purchase_price")]
        public int? PurchasePrice { get; set; }

        [JsonPropertyName("sell_price")]
        public int? SellPrice { get; set; }

        [JsonPropertyName("currency")]
        public required PkResource Currency { get; set; }

        [JsonPropertyName("version_group")]
        public required PkResource VersionGroup { get; set; }
    }

    public class HeldPokemon : IPkResource
    {
        [JsonPropertyName("pokemon")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("version_details")]
        public HeldVersion[] Versions { get; set; } = [];
    }

    public class HeldVersion : IPkResource
    {
        [JsonPropertyName("rarity")]
        public int Rarity { get; set; }

        [JsonPropertyName("version")]
        public required PkResource Resource { get; set; }
    }

    public class Machine : IPkResource
    {
        [JsonPropertyName("machine")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("version_group")]
        public required PkResource VersionGroup { get; set; }
    }
}
