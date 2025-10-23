namespace DndSharp.Pokemon;

using Evolution = (PkPokemonSpecies Pokemon, PkEvolutionChain.EvolutionDetails Requirements);
using Move = (PkMove Move, PkMoveLearnMethod Method, PkResource Game, int LearnLevel);
using VarientType = (PkPokemon Pokemon, bool IsDefault);

/// <summary>
/// Represents a serializable cache of a specific Pokémon and it's evolutions
/// </summary>
public class PokemonCache
{
    private VarientType[]? _varients;
    private Evolution[]? _evolutions;
    private Move[]? _moves;
    private int? _speciesId;
    private int? _growhRateId;
    private PkPokemon? _defaultForm;

    /// <summary>
    /// The ID of the selected Pokémon.
    /// </summary>
    /// <remarks>Setting this manually requires validating that it exists in <see cref="InternalSpecies"/> or it can cause issues</remarks>
    [JsonPropertyName("selectedId"), JsonInclude]
    public int PokemonId { get; internal set; }

    [JsonPropertyName("pokemon"), JsonInclude]
    internal Dictionary<int, PkPokemon> InternalPokemon { get; set; } = [];

    [JsonPropertyName("species"), JsonInclude]
    internal Dictionary<int, PkPokemonSpecies> InternalSpecies { get; set; } = [];

    [JsonPropertyName("growthRates"), JsonInclude]
    internal Dictionary<int, PkGrowthRate> InternalGrowthRates { get; set; } = [];

    [JsonPropertyName("moves"), JsonInclude]
    internal Dictionary<int, PkMove> InternalMoves { get; set; } = [];

    [JsonPropertyName("learnMethods"), JsonInclude]
    internal Dictionary<int, PkMoveLearnMethod> InternalLearnMethods { get; set; } = [];

    [JsonPropertyName("evolutions"), JsonInclude]
    internal Dictionary<int, PkEvolutionChain> InternalEvolutions { get; set; } = [];

    /// <summary>
    /// The currently selected Pokémon
    /// </summary>
    /// <remarks>This can be any variant or evolution related to the cached Pokémon</remarks>
    [JsonIgnore]
    public PkPokemon Pokemon
    {
        get => InternalPokemon[PokemonId];
        set
        {
            if (!InternalPokemon.ContainsKey(value.Id))
                throw new ArgumentException($"Pokemon not found in cache >> {value.Name} ({value.Id}). " +
                    $"Are you sure it's related to the current pokemon?", nameof(value));

            PokemonId = value.Id;
            ResetCache();
        }
    }

    /// <summary>
    /// The default form for the current Pokémon
    /// </summary>
    [JsonIgnore]
    public PkPokemon DefaultForm => _defaultForm ??= GetDefaultForm(PokemonId);

    /// <summary>
    /// The species of the currently selected Pokémon
    /// </summary>
    [JsonIgnore]
    public PkPokemonSpecies Species => InternalSpecies[_speciesId ??= Pokemon.Species.Id];

    /// <summary>
    /// Whether or not the selected Pokémon is the default form for it's species
    /// </summary>
    [JsonIgnore]
    public bool IsDefaultForm => Pokemon.IsDefault;

    /// <summary>
    /// All of the varients of the currently selected Pokémon
    /// </summary>
    [JsonIgnore]
    public VarientType[] Varients => _varients ??= [.. GetVarients()];

    /// <summary>
    /// The growth rate for the current Pokémon
    /// </summary>
    /// <remarks>You cannot cacess <see cref="PkGrowthRate.Species"/> from this reference (it will always be empty)</remarks>
    [JsonIgnore]
    public PkGrowthRate GrowthRate => InternalGrowthRates[_growhRateId ??= Species.GrowthRate.Id];

    /// <summary>
    /// The EXP gain formula for the current pokemon
    /// </summary>
    [JsonIgnore]
    public ExperienceGain ExpGainFormula => (ExperienceGain)GrowthRate.Id;

    /// <summary>
    /// All of the evolutions for the current pokemon
    /// </summary>
    [JsonIgnore]
    public Evolution[] Evolutions => _evolutions ??= [.. GetEvolutions()];

    /// <summary>
    /// All of the current pokemons available moves
    /// </summary>
    /// <remarks>You cannot access <see cref="PkMove.LearnedBy"/> from this reference (it will always be empty)</remarks>
    [JsonIgnore]
    public Move[] Moves => _moves ??= [.. GetMoves()];

    /// <summary>
    /// All of the pokemon related to the current one
    /// </summary>
    [JsonIgnore]
    public IEnumerable<PkPokemon> RelatedPokemon => InternalPokemon.Values;

    /// <summary>
    /// All of the species related to the current pokemon
    /// </summary>
    [JsonIgnore]
    public IEnumerable<PkPokemonSpecies> RelatedSpecies => InternalSpecies.Values;

    internal void ResetCache()
    {
        _varients = null;
        _speciesId = null;
        _growhRateId = null;
        _evolutions = null;
        _defaultForm = null;
        _moves = null;
    }

    internal PkPokemon GetDefaultForm(int id)
    {
        var pk = InternalPokemon[id];
        if (pk.IsDefault) return pk;

        return GetVarients().First(t => t.IsDefault).Pokemon;
    }

    internal IEnumerable<VarientType> GetVarients()
    {
        foreach (var varient in Species.Varieties)
        {
            var pid = varient.Resource.Id;
            if (InternalPokemon.TryGetValue(pid, out var pokemon))
                yield return (pokemon, varient.IsDefault);
        }
    }

    internal IEnumerable<Evolution> GetEvolutions()
    {
        foreach(var (_, evolution) in InternalEvolutions)
        {
            var evolutions = PokemonTranslationService.ExpandEvolutions(evolution.Chain);
            foreach (var (from, to, deets) in evolutions)
            {
                if (from != Species.Id) continue;

                var species = InternalSpecies[to];
                yield return (species, deets);
            }
        }
    }

    internal IEnumerable<Move> GetMoves()
    {
        foreach(var move in Pokemon.Moves)
        {
            var moveId = move.Resource.Id;
            var fullMove = InternalMoves[moveId];

            foreach(var deet in move.Versions)
            {
                var methodId = deet.Method.Id;
                var method = InternalLearnMethods[methodId];

                yield return (fullMove, method, deet.Group, deet.Level);
            }
        }
    }
}
