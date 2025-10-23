namespace DndSharp.PokeDex.Models.Item;

using Meta;

[PkResource("machine/([0-9]{1,})")]
public class PkMachine
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("item")]
    public required PkResource Item { get; set; }

    [JsonPropertyName("move")]
    public required PkResource Move { get; set; }

    [JsonPropertyName("version_group")]
    public required PkResource VersionGroup { get; set; }
}
