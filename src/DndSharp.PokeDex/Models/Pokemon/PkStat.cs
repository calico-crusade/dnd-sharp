namespace DndSharp.PokeDex.Models.Pokemon;

using Meta;

[PkResource("stat/([0-9]{1,})")]
public class PkStat : PkLocalizedBase
{
    [JsonPropertyName("game_index")]
    public int GameIndex { get; set; }

    [JsonPropertyName("is_battle_only")]
    public bool IsBattleOnly { get; set; }

    [JsonPropertyName("move_damage_class")]
    public PkResource? DamageClass { get; set; }

    [JsonPropertyName("characteristics")]
    public PkResource[] Characteristics { get; set; } = [];

    [JsonPropertyName("affecting_natures")]
    public required IncreaseDecrease<PkResource> Natures { get; set; }

    [JsonPropertyName("affecting_moves")]
    public required IncreaseDecrease<Move> Moves { get; set; }

    public class IncreaseDecrease<T>
    {
        [JsonPropertyName("decrease")]
        public T[] Decrease { get; set; } = [];

        [JsonPropertyName("increase")]
        public T[] Increase { get; set; } = [];
    }

    public class Move : IPkResource
    {
        [JsonPropertyName("change")]
        public int Change { get; set; }

        [JsonPropertyName("move")]
        public required PkResource Resource { get; set; }
    }
}
