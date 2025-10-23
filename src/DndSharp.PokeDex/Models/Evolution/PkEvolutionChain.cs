namespace DndSharp.PokeDex.Models.Evolution;

using Meta;

[PkResource("evolution-chain/([0-9]{1,})")]
public class PkEvolutionChain
{
    [JsonPropertyName("baby_trigger_item")]
    public PkResource? BabyTriggerItem { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("chain")]
    public required ChainDetails Chain { get; set; }

    public class ChainDetails
    {
        [JsonPropertyName("evolution_details")]
        public EvolutionDetails[] EvolutionDetails { get; set; } = [];

        [JsonPropertyName("evolves_to")]
        public ChainDetails[] EvolvesTo { get; set; } = [];

        [JsonPropertyName("is_baby")]
        public bool IsBaby { get; set; }

        [JsonPropertyName("species")]
        public required PkResource Species { get; set; }
    }

    public class EvolutionDetails
    {
        [JsonPropertyName("gender")]
        public int? Gender { get; set; }

        [JsonPropertyName("held_item")]
        public PkResource? HeldItem { get; set; }

        [JsonPropertyName("item")]
        public PkResource? Item { get; set; }

        [JsonPropertyName("known_move")]
        public PkResource? KnownMove { get; set; }

        [JsonPropertyName("known_move_type")]
        public PkResource? KnownMoveType { get; set; }

        [JsonPropertyName("location")]
        public PkResource? Location { get; set; }

        [JsonPropertyName("min_affection")]
        public int? MinAffection { get; set; }

        [JsonPropertyName("min_beauty")]
        public int? MinBeauty { get; set; }

        [JsonPropertyName("min_happiness")]
        public int? MinHappiness { get; set; }

        [JsonPropertyName("min_level")]
        public int? MinLevel { get; set; }

        [JsonPropertyName("needs_overworld_rain")]
        public bool NeedsOverworldRain { get; set; }

        [JsonPropertyName("party_species")]
        public PkResource? PartySpecies { get; set; }

        [JsonPropertyName("party_type")]
        public PkResource? PartyType { get; set; }

        [JsonPropertyName("relative_physical_stats")]
        public int? RelativePhysicalStats { get; set; }

        [JsonPropertyName("time_of_day")]
        public string? TimeOfDay { get; set; }

        [JsonPropertyName("trade_species")]
        public PkResource? TradeSpecies { get; set; }

        [JsonPropertyName("trigger")]
        public required PkResource Trigger { get; set; }

        [JsonPropertyName("turn_upside_down")]
        public bool TurnUpsideDown { get; set; }
    }
}
