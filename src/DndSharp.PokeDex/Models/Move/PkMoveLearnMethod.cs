namespace DndSharp.PokeDex.Models.Move;

using Meta;

[PkResource("move-learn-method/([0-9]{1,})")]
public class PkMoveLearnMethod : PkLocalizedDescBase
{
    [JsonPropertyName("version_groups")]
    public PkResource[] Versions { get; set; } = [];
}
