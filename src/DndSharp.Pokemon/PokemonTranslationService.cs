using System.Collections.Concurrent;

namespace DndSharp.Pokemon;

using Dice;
using Dice.NumberGenerator;

public interface IPokemonTranslationService
{
    Task<PokemonCharacter?> Translate(int pokemonId, TranslateSettings? settings = null, CancellationToken token = default);

    Task<PokemonEVs> RandomizeEVs(INumberGenerator? generator = null, CancellationToken token = default);

    Task<PokemonIVs> RandomizeIVs(INumberGenerator? generator = null, CancellationToken token = default);

    Task<PkNature> RandomNature(INumberGenerator? generator = null, CancellationToken token = default);
}

internal class PokemonTranslationService(
    IPokeApiService _poke,
    ILogger<PokemonTranslationService> _logger) : IPokemonTranslationService
{
    private readonly ConcurrentDictionary<int, PokemonCache> _cache = [];

    public static IEnumerable<Evolution> ExpandEvolutions(PkEvolutionChain.ChainDetails chain)
    {
        var from = chain.Species.Id;
        foreach (var item in chain.EvolvesTo)
        {
            var to = item.Species.Id;
            foreach (var deet in item.EvolutionDetails)
                yield return new(from, to, deet);

            foreach (var recurse in ExpandEvolutions(item))
                yield return recurse;
        }
    }

    public async Task<PokemonCache?> GetCache(int id, CancellationToken token)
    {
        async Task LoadSpecies(PokemonCache cache, int id)
        {
            //If the species is already loaded, skip it
            if (cache.InternalSpecies.ContainsKey(id))
                return;

            var species = await _poke.Fetch<PkPokemonSpecies>(id, token: token);
            if (species is null)
            {
                _logger.LogInformation("Could not find species for pokemon id: {id}", id);
                return;
            }

            //Cache the species
            cache.InternalSpecies[species.Id] = species;

            //Load all varieties of this species
            foreach (var v in species.Varieties)
                await LoadPokemon(cache, v.Resource.Id);

            //Load the species it evolves from
            if (species.EvolvesFrom is not null)
                await LoadSpecies(cache, species.EvolvesFrom.Id);

            //Load the evolution chain
            if (species.EvolutionChain is null) return;

            var eid = species.EvolutionChain.Id;
            if (cache.InternalEvolutions.ContainsKey(eid))
                return;

            var evolution = await _poke.Fetch<PkEvolutionChain>(eid, token: token);
            if (evolution is null)
            {
                _logger.LogInformation("Could not find evolution chain for species id: {id} >> {url}", 
                    id, species.EvolutionChain.Url);
                return;
            }
            //Set the evolution chain in the cache
            cache.InternalEvolutions[eid] = evolution;
            //Load all species in the evolution chain
            var evolutions = ExpandEvolutions(evolution.Chain).ToArray();
            foreach (var (from, to, _) in evolutions)
            {
                await LoadSpecies(cache, from);
                await LoadSpecies(cache, to);
            }
        }

        async Task LoadPokemon(PokemonCache cache, int id)
        {
            //If the pokemon is already loaded, skip it
            if (cache.InternalPokemon.ContainsKey(id))
                return;

            var pokemon = await _poke.Fetch<PkPokemon>(id, token: token);
            if (pokemon is null)
            {
                _logger.LogInformation("Could not find pokemon with id: {id}", id);
                return;
            }
            //Add the pokemon to the cache
            cache.InternalPokemon[id] = pokemon;
            //Load the species for this pokemon
            await LoadSpecies(cache, pokemon.Species.Id);
        }

        async Task LoadMetaData(PokemonCache cache)
        {
            //Load all growth rates for the species in the cache
            foreach(var (_, species) in cache.InternalSpecies)
            {
                if (cache.InternalGrowthRates.ContainsKey(species.GrowthRate.Id))
                    continue;

                var rate = await _poke.Fetch<PkGrowthRate>(species.GrowthRate.Url, token: token);
                if (rate is null)
                {
                    _logger.LogInformation("Could not find growth rate: {name}", species.GrowthRate.Name);
                    continue;
                }

                rate.Species = [];
                cache.InternalGrowthRates[rate.Id] = rate;
            }

            //Load all of the moves
            var moves = cache.InternalPokemon.Values
                .SelectMany(t => t.Moves)
                .DistinctBy(t => t.Resource.Id);
            foreach (var move in moves)
            {
                var mid = move.Resource.Id;
                if (cache.InternalMoves.ContainsKey(mid))
                    continue;

                var pkMove = await _poke.Fetch<PkMove>(mid, token: token);
                if (pkMove is null)
                {
                    _logger.LogInformation("Could not find move: {id}", mid);
                    continue;
                }

                pkMove.LearnedBy = [];
                cache.InternalMoves[pkMove.Id] = pkMove;

                //Load all of the move learn methods
                foreach (var version in move.Versions)
                {
                    var methodId = version.Method.Id;
                    if (cache.InternalLearnMethods.ContainsKey(methodId))
                        continue;

                    var method = await _poke.Fetch<PkMoveLearnMethod>(methodId, token: token);
                    if (method is null)
                    {
                        _logger.LogInformation("Could not find move learn method: {id}", methodId);
                        continue;
                    }

                    cache.InternalLearnMethods[method.Id] = method;
                }
            }
        }

        if (_cache.TryGetValue(id, out var cache))
        {
            cache = _poke.Clone(cache);
            cache.PokemonId = id;
            return cache;
        }

        cache = new PokemonCache { PokemonId = id };
        await LoadPokemon(cache, id);
        await LoadMetaData(cache);

        foreach (var mon in cache.RelatedPokemon)
            _cache[mon.Id] = cache;
        return _poke.Clone(cache);
    }

    public async Task<PokemonCharacter?> Translate(int pokemonId, TranslateSettings? settings = null, CancellationToken token = default)
    {
        var cache = await GetCache(pokemonId, token);
        return cache is null ? null : new()
        {
            Data = cache,
            Settings = settings ?? new TranslateSettings(),
            EVs = await RandomizeEVs(token: token),
            IVs = await RandomizeIVs(token: token),
            Nature = await RandomNature(token: token)

        };
    }

    public static async Task Randomize(int total, int max, Action<int>[] settings, INumberGenerator? generator = null, CancellationToken token = default)
    {
        int remaining = total;
        generator ??= Die.DefaultNumberGenerator;

        var set = settings.ToList();
        while(set.Count > 0 && remaining > 0)
        {
            var index = set.Count > 1 ? await generator.Generate(set.Count - 1, token) : 0;
            var statSetter = set[index];
            set.RemoveAt(index);

            var available = Math.Min(remaining, max);
            if (available == 0) break;

            if (available == 1)
            {
                statSetter(1);
                remaining -= 1;
                continue;
            }

            var value = await generator.Generate(0, available, token);
            statSetter(value);
            remaining -= value;
        }
    }

    public async Task<PokemonEVs> RandomizeEVs(INumberGenerator? generator = null, CancellationToken token = default)
    {
        var stats = new PokemonStatsInt();
        Action<int>[] settings =
        [
            (i) => stats.Hp = i,
            (i) => stats.Attack = i,
            (i) => stats.Defense = i,
            (i) => stats.SpecialAttack = i,
            (i) => stats.SpecialDefense = i,
            (i) => stats.Speed = i
        ];

        await Randomize(PokemonEVs.MAX_TOTAL, PokemonEVs.MAX_STAT, settings, generator, token);
        return stats;
    }

    public async Task<PokemonIVs> RandomizeIVs(INumberGenerator? generator = null, CancellationToken token = default)
    {
        var stats = new PokemonStatsInt();
        Action<int>[] settings =
        [
            (i) => stats.Hp = i,
            (i) => stats.Attack = i,
            (i) => stats.Defense = i,
            (i) => stats.SpecialAttack = i,
            (i) => stats.SpecialDefense = i,
            (i) => stats.Speed = i
        ];

        await Randomize(PokemonIVs.MAX_STAT * settings.Length, PokemonIVs.MAX_STAT, settings, generator, token);
        return stats;
    }

    public async Task<PkNature> RandomNature(INumberGenerator? generator = null, CancellationToken token = default)
    {
        var natures = await _poke.Index<PkNature>(token: token);
        if (natures is null || natures.Results.Length == 0)
            throw new InvalidOperationException("Could not retrieve natures from PokeAPI");

        generator ??= Die.DefaultNumberGenerator;
        var index = await generator.Generate(natures.Results.Length - 1, token);
        var nid = natures.Results[index].Id;
        var nature = await _poke.Fetch<PkNature>(nid, token: token);
        return nature ?? throw new InvalidOperationException($"Could not retrieve nature with id: {nid} from PokeAPI");
    }

    public record class Evolution(
        [property: JsonPropertyName("from")] int From,
        [property: JsonPropertyName("to")] int To,
        [property: JsonPropertyName("deets")] PkEvolutionChain.EvolutionDetails Deets);
}
