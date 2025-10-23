namespace DndSharp.PokeDex.Models.Meta;

public class PkLocalization : IPkResource
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("language")]
    public required PkResource Resource { get; set; }
}

public class PkLocalizedDescription : IPkResource
{
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("language")]
    public required PkResource Resource { get; set; }
}