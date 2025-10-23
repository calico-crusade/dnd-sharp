namespace DndSharp.PokeDex.Models.Item;

using Meta;

[PkResource("item-attribute/([0-9]{1,})")]
public class PkItemAttribute : PkLocalizedDescBase
{
    [JsonPropertyName("items")]
    public PkResource[] Items { get; set; } = [];
}
