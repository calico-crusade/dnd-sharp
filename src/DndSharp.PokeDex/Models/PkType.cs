namespace DndSharp.PokeDex.Models;

using Meta;

[PkResource("type/([0-9]{1,})")]
public class PkType : PkLocalizedBase
{
    [JsonPropertyName("damage_relations")]
    public required Damage DamageRelations { get; set; }

    [JsonPropertyName("game_indices")]
    public GameIndex[] GameIndices { get; set; } = [];

    [JsonPropertyName("generation")]
    public required PkResource Generation { get; set; }

    [JsonPropertyName("move_damage_class")]
    public PkResource? MoveDamageClass { get; set; }

    [JsonPropertyName("moves")]
    public PkResource[] Moves { get; set; } = [];

    [JsonPropertyName("pokemon")]
    public PokemonRef[] Pokemon { get; set; } = [];

    [JsonPropertyName("past_damage_relations")]
    public PastDamage[] PastDamageRelations { get; set; } = [];

    public class Damage
    {
        [JsonPropertyName("double_damage_from")]
        public PkResource[] DoubleFrom { get; set; } = [];

        [JsonPropertyName("double_damage_to")]
        public PkResource[] DoubleTo { get; set; } = [];

        [JsonPropertyName("half_damage_from")]
        public PkResource[] HalfFrom { get; set; } = [];

        [JsonPropertyName("half_damage_to")]
        public PkResource[] HalfTo { get; set; } = [];

        [JsonPropertyName("no_damage_from")]
        public PkResource[] NoFrom { get; set; } = [];

        [JsonPropertyName("no_damage_to")]
        public PkResource[] NoTo { get; set; } = [];
    }

    public class GameIndex : IPkResource
    {
        [JsonPropertyName("game_index")]
        public int Index { get; set; }

        [JsonPropertyName("generation")]
        public required PkResource Resource { get; set; }
    }

    public class PokemonRef : IPkResource
    {
        [JsonPropertyName("pokemon")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("slot")]
        public int Slot { get; set; }
    }

    public class PastDamage : IPkResource
    {
        [JsonPropertyName("damage_relations")]
        public required Damage DamageRelations { get; set; }

        [JsonPropertyName("generation")]
        public required PkResource Resource { get; set; }
    }
}
