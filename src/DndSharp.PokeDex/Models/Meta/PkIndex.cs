namespace DndSharp.PokeDex.Models.Meta;

public class PkIndex
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("next")]
    public int? Next { get; set; }

    [JsonPropertyName("previous")]
    public int? Previous { get; set; }

    [JsonPropertyName("results")]
    public PkResource[] Results { get; set; } = [];
}
