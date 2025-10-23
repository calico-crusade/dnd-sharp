namespace DndSharp.PokeDex.Models.Meta;

public class PkResource
{
    private int? _id;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public required string Url { get; set; }

    [JsonIgnore]
    public int Id => _id ??= GetId();

    internal int GetId()
    {
        var id = Url
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => int.TryParse(t, out var res) ? res : (int?)null)
            .FirstOrDefault(t => t is not null);
        if (id is null || id == default)
            throw new InvalidOperationException($"Resource does not have a valid id: {Url}");
        return id.Value;
    }
}

public class PkResourceMatch : PkResource
{
    [JsonIgnore]
    public int Score { get; set; }
}

public interface IPkResource
{
    public PkResource Resource { get; set; }
}