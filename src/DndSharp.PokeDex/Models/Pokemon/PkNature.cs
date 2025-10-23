namespace DndSharp.PokeDex.Models.Pokemon;

using Meta;

[PkResource("nature/([0-9]{1,})")]
public class PkNature : PkLocalizedBase
{
    [JsonPropertyName("decreased_stat")]
    public PkResource? DecreasedStat { get; set; }

    [JsonPropertyName("increased_stat")]
    public PkResource? IncreasedStat { get; set; }

    [JsonPropertyName("hates_flavor")]
    public PkResource? HatesFlavor { get; set; }

    [JsonPropertyName("likes_flavor")]
    public PkResource? LikesFlavor { get; set; }

    [JsonPropertyName("move_battle_style_preferences")]
    public Preference[] Preferences { get; set; } = [];

    [JsonPropertyName("pokeathlon_stat_changes")]
    public StatChange[] PokeathlonStatChanges { get; set; } = [];

    public class Preference : IPkResource
    {
        [JsonPropertyName("high_hp_preference")]
        public int HighHpPreference { get; set; }

        [JsonPropertyName("low_hp_preference")]
        public int LowHpPreference { get; set; }

        [JsonPropertyName("move_battle_style")]
        public required PkResource Resource { get; set; }
    }

    public class StatChange : IPkResource
    {
        [JsonPropertyName("max_change")]
        public int MaxChange { get; set; }

        [JsonPropertyName("pokeathlon_stat")]
        public required PkResource Resource { get; set; }
    }
}
