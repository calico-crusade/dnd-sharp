namespace DndSharp.PokeDex.Models.Pokemon;

public class PkSprite : PkSpriteBase
{
    [JsonPropertyName("other")]
    public Dictionary<string, PkSpriteBase> Other { get; set; } = [];

    [JsonPropertyName("versions")]
    public Dictionary<string, Dictionary<string, PkSpriteVersion>> Versions { get; set; } = [];
}

public class PkSpriteVersion : PkSpriteBase
{
    [JsonPropertyName("animated")]
    public PkSpriteBase? Animated { get; set; }
}

public class PkSpriteBase
{
    [JsonPropertyName("back_default")]
    public string? BackDefault { get; set; }

    [JsonPropertyName("back_female")]
    public string? BackFemale { get; set; }

    [JsonPropertyName("back_shiny")]
    public string? BackShiny { get; set; }

    [JsonPropertyName("back_shiny_female")]
    public string? BackShinyFemale { get; set; }

    [JsonPropertyName("front_default")]
    public string? FrontDefault { get; set; }

    [JsonPropertyName("front_female")]
    public string? FrontFemale { get; set; }

    [JsonPropertyName("front_shiny")]
    public string? FrontShiny { get; set; }

    [JsonPropertyName("front_shiny_female")]
    public string? FrontShinyFemale { get; set; }
}
