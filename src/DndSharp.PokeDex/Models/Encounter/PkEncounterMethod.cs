namespace DndSharp.PokeDex.Models.Encounter;

using Meta;

[PkResource("encounter-method/([0-9]{1,})")]
public class PkEncounterMethod : PkLocalizedBase
{
    [JsonPropertyName("order")]
    public int Order { get; set; }
}
