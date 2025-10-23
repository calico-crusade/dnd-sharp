namespace DndSharp.PokeDex.Models.Pokemon;

using Meta;

[PkResource("pokemon-form/([0-9]{1,})")]
public class PkPokemonForm : PkLocalizedBase
{
    [JsonPropertyName("form_name")]
    public required string FormName { get; set; }

    [JsonPropertyName("form_names")]
    public PkLocalization[] FormNames { get; set; } = [];

    [JsonPropertyName("form_order")]
    public int FormOrder { get; set; }

    [JsonPropertyName("is_battle_only")]
    public bool IsBattleOnly { get; set; }

    [JsonPropertyName("is_default")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("is_mega")]
    public bool IsMega { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("pokemon")]
    public required PkResource Pokemon { get; set; }

    [JsonPropertyName("sprites")]
    public required PkSprite Sprites { get; set; }

    [JsonPropertyName("types")]
    public Type[] Types { get; set; } = [];

    [JsonPropertyName("version_group")]
    public required PkResource VersionGroup { get; set; }

    public class Type : IPkResource
    {
        [JsonPropertyName("slot")]
        public int Slot { get; set; }

        [JsonPropertyName("type")]
        public required PkResource Resource { get; set; }
    }
}
