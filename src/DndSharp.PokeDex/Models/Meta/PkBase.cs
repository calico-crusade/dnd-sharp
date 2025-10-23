namespace DndSharp.PokeDex.Models.Meta;

public abstract class PkBase
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public virtual required string Name { get; set; }
}

public abstract class PkLocalizedBase : PkBase
{
    [JsonPropertyName("names")]
    public virtual PkLocalization[] Localizations { get; set; } = [];
}

public abstract class PkLocalizedDescBase : PkLocalizedBase
{
    [JsonPropertyName("descriptions")]
    public virtual PkLocalizedDescription[] Descriptions { get; set; } = [];
}