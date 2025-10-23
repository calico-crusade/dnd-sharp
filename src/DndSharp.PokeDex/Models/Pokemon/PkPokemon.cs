namespace DndSharp.PokeDex.Models.Pokemon;

using Meta;

[PkResource("pokemon/([0-9]{1,})")]
public class PkPokemon : PkBase
{
    [JsonPropertyName("abilities")]
    public Ability[] Abilities { get; set; } = [];

    [JsonPropertyName("cries")]
    public Dictionary<string, string> Cries { get; set; } = [];

    [JsonPropertyName("forms")]
    public PkResource[] Forms { get; set; } = [];

    [JsonPropertyName("game_indices")]
    public Game[] Games { get; set; } = [];

    [JsonPropertyName("moves")]
    public Move[] Moves { get; set; } = [];

    [JsonPropertyName("base_experience")]
    public int BaseExperience { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("width")]
    public int Weight { get; set; }

    [JsonPropertyName("is_default")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("location_area_encounters")]
    public required string Encounters { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("species")]
    public required PkResource Species { get; set; }

    [JsonPropertyName("sprites")]
    public required PkSprite Sprites { get; set; }

    [JsonPropertyName("stats")]
    public Stat[] Stats { get; set; } = [];

    [JsonPropertyName("types")]
    public Type[] Types { get; set; } = [];

    [JsonPropertyName("held_items")]
    public Item[] HeldItems { get; set; } = [];

    public class Stat : IPkResource
    {
        [JsonPropertyName("base_stat")]
        public int BaseStat { get; set; }

        [JsonPropertyName("effort")]
        public int Effort { get; set; }

        [JsonPropertyName("stat")]
        public required PkResource Resource { get; set; }
    }

    public class Ability : IPkResource
    {
        [JsonPropertyName("ability")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("is_hidden")]
        public bool IsHidden { get; set; }

        [JsonPropertyName("slot")]
        public int Slot { get; set; }
    }

    public class Game : IPkResource
    {
        [JsonPropertyName("game_index")]
        public int Index { get; set; }

        [JsonPropertyName("version")]
        public required PkResource Resource { get; set; }
    }

    public class Move : IPkResource
    {
        [JsonPropertyName("move")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("version_group_details")]
        public MoveVersionGroup[] Versions { get; set; } = [];
    }

    public class Item : IPkResource
    {
        [JsonPropertyName("item")]
        public required PkResource Resource { get; set; }

        [JsonPropertyName("version_details")]
        public ItemVersionGroup[] Versions { get; set; } = [];
    }

    public class MoveVersionGroup
    {
        [JsonPropertyName("level_learned_at")]
        public int Level { get; set; }

        [JsonPropertyName("move_learn_method")]
        public required PkResource Method { get; set; }

        [JsonPropertyName("version_group")]
        public required PkResource Group { get; set; }
    }

    public class ItemVersionGroup : IPkResource
    {
        [JsonPropertyName("rarity")]
        public int Rarity { get; set; }

        [JsonPropertyName("version")]
        public required PkResource Resource { get; set; }
    }

    public class Type : IPkResource
    {
        [JsonPropertyName("slot")]
        public int Slot { get; set; }

        [JsonPropertyName("type")]
        public required PkResource Resource { get; set; }
    }
}

