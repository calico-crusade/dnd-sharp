namespace DndSharp.PokeDex.Models.Pokemon;

using Meta;

[PkResource("growth-rate/([0-9]{1,})")]
public class PkGrowthRate : PkBase
{
    [JsonPropertyName("descriptions")]
    public PkLocalizedDescription[] Descriptions { get; set; } = [];

    [JsonPropertyName("formula")]
    public required string Formula { get; set; }

    [JsonPropertyName("levels")]
    public Level[] Levels { get; set; } = [];

    [JsonPropertyName("pokemon_species")]
    public PkResource[] Species { get; set; } = [];

    public class Level
    {
        [JsonPropertyName("experience")]
        public int Experience { get; set; }

        [JsonPropertyName("level")]
        public int Value { get; set; }
    }

}
