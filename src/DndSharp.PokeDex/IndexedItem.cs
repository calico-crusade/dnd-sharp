namespace DndSharp.PokeDex;

using Models.Meta;

public interface IIndexedItem<T>
{
    Task<PkIndex?> Index(bool? cache = null, CancellationToken token = default);

    IAsyncEnumerable<T> All(bool? cache = null, CancellationToken token = default);

    IAsyncEnumerable<T> All(IAsyncEnumerable<PkResource> resources, bool? cache = null, CancellationToken token = default);

    IAsyncEnumerable<PkResource> AllResources(bool? cache = null, CancellationToken token = default);

    IAsyncEnumerable<(T item, int score)> Search(string term, int? cutoff = null, bool? cache = null, CancellationToken token = default);

    IAsyncEnumerable<PkResourceMatch> SearchResources(string term, int? cutoff = null, bool? cache = null, CancellationToken token = default);
}

internal class IndexedItem<T>(
    IPokeApiService _api) : IIndexedItem<T> where T : class
{
    private PkIndex? _index;

    public async Task<PkIndex?> Index(bool? cache = null, CancellationToken token = default)
    {
        return _index ??= await _api.Index<T>(cache, token);
    }

    public async IAsyncEnumerable<PkResource> AllResources(bool? cache = null, [EnumeratorCancellation] CancellationToken token = default)
    {
        var index = await Index(cache, token);
        if (index is null ||
            index.Results is null ||
            index.Results.Length == 0) yield break;

        foreach (var item in index.Results)
            yield return item;
    }

    public async IAsyncEnumerable<T> All(IAsyncEnumerable<PkResource> resources, bool? cache = null, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach(var item in resources)
        {
            var resource = await _api.Fetch<T>(item.Url, cache, token);
            if (resource is not null)
                yield return resource;
        }
    }

    public IAsyncEnumerable<T> All(bool? cache = null, CancellationToken token = default)
    {
        var all = AllResources(cache, token);
        return All(all, cache, token);
    }

    public async IAsyncEnumerable<(T item, int score)> Search(string term, int? cutoff = null, bool? cache = null, [EnumeratorCancellation] CancellationToken token = default)
    {
        var filtered = SearchResources(term, cutoff, cache, token)
            .OrderByDescending(t => t.Score);
        await foreach (var match in filtered)
        {
            var resource = await _api.Fetch<T>(match.Url, cache, token);
            if (resource is not null)
                yield return (resource, match.Score);
        }
    }

    public async IAsyncEnumerable<PkResourceMatch> SearchResources(string term, int? cutoff = null, bool? cache = null, [EnumeratorCancellation] CancellationToken token = default)
    {
        var all = AllResources(cache, token);
        var filtered = all.Search(term, t => t.Name ?? string.Empty, cutoff: cutoff);
        await foreach (var item in filtered)
            yield return new PkResourceMatch
            {
                Url = item.Value.Url,
                Name = item.Value.Name,
                Score = item.Score
            };
    }
}
