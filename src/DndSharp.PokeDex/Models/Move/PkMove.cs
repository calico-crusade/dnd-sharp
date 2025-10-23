namespace DndSharp.PokeDex.Models.Move;

using Meta;

[PkResource("move/([0-9]{1,})")]
public class PkMove : PkLocalizedBase
{
    [JsonPropertyName("accuracy")]
    public int? Accuracy { get; set; }

    [JsonPropertyName("power")]
    public int? Power { get; set; }

    [JsonPropertyName("pp")]
    public int? Pp { get; set; }

    [JsonPropertyName("priority")]
    public int? Priority { get; set; }

    [JsonPropertyName("effect_chance")]
    public int? EffectChance { get; set; }

    [JsonPropertyName("contest_combos")]
    public Combos? ContestCombos { get; set; }

    [JsonPropertyName("contest_effect")]
    public PkResource? ContestEffect { get; set; }

    [JsonPropertyName("contest_type")]
    public PkResource? ContestType { get; set; }

    [JsonPropertyName("damage_class")]
    public PkResource? DamageClass { get; set; }

    [JsonPropertyName("type")]
    public PkResource? Type { get; set; }

    [JsonPropertyName("target")]
    public PkResource? Target { get; set; }

    [JsonPropertyName("super_contest_effect")]
    public PkResource? SuperContestEffect { get; set; }

    [JsonPropertyName("effect_entries")]
    public EffectEntry[] EffectEntries { get; set; } = [];

    [JsonPropertyName("effect_changes")]
    public EffectChange[] EffectChanges { get; set; } = [];

    [JsonPropertyName("flavor_text_entries")]
    public FlavorText[] FlavorTextEntries { get; set; } = [];

    [JsonPropertyName("generation")]
    public PkResource Generation { get; set; }

    [JsonPropertyName("learned_by_pokemon")]
    public PkResource[] LearnedBy { get; set; } = [];

    [JsonPropertyName("machines")]
    public Machine[] Machines { get; set; } = [];

    [JsonPropertyName("meta")]
    public Metadata? Meta { get; set; }

    [JsonPropertyName("stat_changes")]
    public StatChange[] StatChanges { get; set; } = [];

    public class Metadata
    {
        [JsonPropertyName("ailment")]
        public required PkResource Ailment { get; set; }

        [JsonPropertyName("ailment_chance")]
        public int AilmentChance { get; set; }

        [JsonPropertyName("category")]
        public required PkResource Category { get; set; }

        [JsonPropertyName("crit_rate")]
        public int CritRate { get; set; }

        [JsonPropertyName("drain")]
        public int Drain { get; set; }

        [JsonPropertyName("flinch_chance")]
        public int FlinchChance { get; set; }

        [JsonPropertyName("healing")]
        public int Healing { get; set; }

        [JsonPropertyName("stat_chance")]
        public int StatChance { get; set; }

        [JsonPropertyName("max_hits")]
        public int? MaxHits { get; set; }

        [JsonPropertyName("max_turns")]
        public int? MaxTurns { get; set; }

        [JsonPropertyName("min_hits")]
        public int? MinHits { get; set; }

        [JsonPropertyName("min_turns")]
        public int? MinTurns { get; set; }
    }

    public class Combos
    {
        public CombosData? Normal { get; set; }

        public CombosData? Super { get; set; }
    }

    public class CombosData
    {
        public PkResource[]? UseAfter { get; set; }

        public PkResource[]? UseBefore { get; set; }
    }

    public class EffectEntry : IPkResource
    {
        [JsonPropertyName("effect")]
        public required string Value { get; set; }

        [JsonPropertyName("language")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("short_effect")]
        public string? ShortEffect { get; set; }
    }

    public class EffectChange : IPkResource
    {
        [JsonPropertyName("effect_entries")]
        public EffectEntry[] Entries { get; set; } = [];

        [JsonPropertyName("version_group")]
        public required PkResource Resource { get; set; }
    }

    public class FlavorText : IPkResource
    {
        [JsonPropertyName("flavor_text")]
        public required string Value { get; set; }

        [JsonPropertyName("language")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("version_group")]
        public required PkResource Version { get; set; }
    }

    public class StatChange : IPkResource
    {
        [JsonPropertyName("change")]
        public int Change { get; set; }

        [JsonPropertyName("stat")]
        public required PkResource Resource { get; set; }
    }

    public class Machine : IPkResource
    {
        [JsonPropertyName("machine")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("version_group")]
        public required PkResource Version { get; set; }
    }
}
