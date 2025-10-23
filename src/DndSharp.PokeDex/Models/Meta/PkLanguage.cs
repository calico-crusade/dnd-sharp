namespace DndSharp.PokeDex.Models.Meta;

[PkResource("language/([0-9]{1,})")]
public class PkLanguage : PkLocalizedBase
{
    [JsonPropertyName("iso3166")]
    public required string CountryCode { get; set; }

    [JsonPropertyName("iso639")]
    public required string Code { get; set; }

    [JsonPropertyName("official")]
    public bool Official { get; set; }
}