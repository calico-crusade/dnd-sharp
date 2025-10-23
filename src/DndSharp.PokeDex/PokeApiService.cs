using System.Collections.Concurrent;

namespace DndSharp.PokeDex;

using Models.Meta;

public interface IPokeApiService
{
    bool ShouldCache { get; set; }

    Task<T?> Fetch<T>(int id, bool? cache = null, CancellationToken token = default);

    Task<T?> Fetch<T>(string path, bool? cache = null, CancellationToken token = default);

    Task<T?> Fetch<T>(PkResource resource, bool? cache = null, CancellationToken token = default);

    Task<T[]> Get<T>(int id, bool? cache = null, CancellationToken token = default);

    Task<T[]> Get<T>(string path, bool? cache = null, CancellationToken token = default);

    Task<T[]> Get<T>(PkResource resource, bool? cache = null, CancellationToken token = default);

    int[] Ids<T>();

    T Clone<T>(T item);

    Task<PkIndex?> Index<T>(bool? cache = null, CancellationToken token = default);

    Task<PkIndex?> Index(string path, bool? cache = null, CancellationToken token = default);

    IIndexedItem<T> Indexed<T>() where T : class;
}

internal class PokeApiService(
    IJsonService _json,
    IConfiguration _config,
    ILogger<PokeApiService> _logger) : IPokeApiService
{
    private TypeCache[]? _attributeCache;
    private string? _path;
    private readonly ConcurrentDictionary<string, object?> _cache = [];

    public bool ShouldCache { get; set; } = true;

    public string BasePath => _path ??= _config["PokeApi:BasePath"] 
        ?? throw new NullReferenceException("PokeApi:BasePath is not found");

    public static string[] PathSplit(string thing)
    {
        return thing.Split(["/", "\\"], StringSplitOptions.RemoveEmptyEntries);
    }

    public string CombinePath(string resource, bool append = true)
    {
        if (string.IsNullOrWhiteSpace(resource)) throw new ArgumentNullException(nameof(resource));

        resource = resource.TrimStart(['/', '\\']).ToLower();
        if (!resource.StartsWith("api/v2/") &&
            !resource.StartsWith("api\\v2\\"))
            resource = $"api/v2/{resource}";

        var parts = PathSplit(BasePath).Concat(PathSplit(resource)).ToArray();
        if (!parts.Last().Equals("index.json") && append)
            parts = [.. parts, "index.json"];

       return Path.Combine(parts);
    }

    public T Clone<T>(T item)
    {
        var json = _json.Serialize(item);
        return _json.Deserialize<T>(json)!;
    }

    public async Task<T?> Fetch<T>(string path, bool? cache = null, CancellationToken token = default)
    {
        try
        {
            cache ??= ShouldCache;
            var resource = CombinePath(path);
            if (cache.Value && _cache.TryGetValue(resource, out var cached))
                return Clone((T?)cached);

            if (!File.Exists(resource)) return default;

            using var stream = File.OpenRead(resource);
            var item = await _json.Deserialize<T>(stream, token);
            if (item is not null && cache.Value)
                _cache[resource] = item;
            return Clone(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't find resource at path {Path}", path);
            return default;
        }
    }

    public Task<T?> Fetch<T>(PkResource resource, bool? cache = null, CancellationToken token = default)
    {
        return Fetch<T>(resource.Url, cache, token);
    }

    public async Task<T?> Fetch<T>(int id, bool? cache = null, CancellationToken token = default)
    {
        var type = Cache<T>();
        if (type is null) return default;

        var path = type.Attribute.Name.Replace("([0-9]{1,})", id.ToString());
        return await Fetch<T>(path, cache, token);
    }

    public Task<PkIndex?> Index(string path, bool? cache = null, CancellationToken token = default)
    {
        return Fetch<PkIndex>(path, cache, token);
    }

    public async Task<PkIndex?> Index<T>(bool? cache = null, CancellationToken token = default)
    {
        var type = Cache<T>();
        if (type is null) return null;

        var path = type.Attribute.Name.Replace("([0-9]{1,})", "").TrimEnd('/');
        return await Index(path, cache, token);
    }

    public async Task<T[]> Get<T>(int id, bool? cache = null, CancellationToken token = default)
    {
        var type = Cache<T>();
        if (type is null) return [];

        var path = type.Attribute.Name.Replace("([0-9]{1,})", id.ToString());
        return await Get<T>(path, cache, token);
    }

    public Task<T[]> Get<T>(PkResource resource, bool? cache = null, CancellationToken token = default)
    {
        return Get<T>(resource.Url, cache, token);
    }

    public Task<T[]> Get<T>(string path, bool? cache = null, CancellationToken token = default)
    {
        return Fetch<T[]>(path, cache, token)
            .ContinueWith(t => t.Result ?? []);
    }

    public TypeCache[] TypeCaches()
    {
        return _attributeCache ??= [..Assembly
            .GetExecutingAssembly()
            .AllTypesWithAttribute<PkResourceAttribute>()
            .Select(t => new TypeCache(t.type, t.attribute))];
    }

    public TypeCache? Cache(Type type)
    {
        var cache = TypeCaches();
        return cache.FirstOrDefault(c => c.Type == type);
    }

    public TypeCache? Cache<T>() => Cache(typeof(T));

    public int[] Ids<T>()
    {
        var cache = Cache<T>();
        if (cache is null) return [];

        var path = cache.Attribute.Name.TrimStart("/api/v2/").Split('/').First();
        var dir = CombinePath(path, false);
        if (!Directory.Exists(dir)) return [];

        var dirs = Directory.GetDirectories(dir);
        return [..dirs
            .Select(d => (int.TryParse(PathSplit(d).Last(), out var res), res))
            .Where(t => t.Item1)
            .Select(t => t.res)
            .OrderBy(i => i)];
    }

    public IIndexedItem<T> Indexed<T>() where T : class
    {
        return new IndexedItem<T>(this);
    }

    public record class TypeCache(Type Type, PkResourceAttribute Attribute);
}

