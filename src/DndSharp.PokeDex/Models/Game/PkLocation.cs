namespace DndSharp.PokeDex.Models.Game;

using Meta;

[PkResource("location/([0-9]{1,})")]
public class PkLocation : PkLocalizedBase
{
    [JsonPropertyName("areas")]
    public PkResource[] Areas { get; set; } = [];

    [JsonPropertyName("region")]
    public required PkResource Region { get; set; }

    [JsonPropertyName("game_indices")]
    public GameIndex[] GameIndices { get; set; } = [];

    public class GameIndex : IPkResource
    {
        [JsonPropertyName("game_index")]
        public int Index { get; set; }

        [JsonPropertyName("generation")]
        public required PkResource Resource { get; set; }
    }
}
