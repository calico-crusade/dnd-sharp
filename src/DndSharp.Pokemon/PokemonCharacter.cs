namespace DndSharp.Pokemon;

public class PokemonCharacter
{
    private string? _name;
    private string? _description;
    private int _level = 1;
    private PokemonBaseStats? _baseStats;
    private DndStats? _dndStats;
    private PokemonEVs? _evs;
    private PokemonIVs? _ivs;
    private PokemonStats? _stats;
    private PkNature? _nature;

    /// <summary>
    /// The name of the character (defaults to the species name)
    /// </summary>
    [JsonPropertyName("name")]
    public string Name
    {
        get => _name ??= SpeciesName;
        set => _name = value;
    }

    /// <summary>
    /// The name of the pokémon current species
    /// </summary>
    [JsonIgnore]
    public string SpeciesName => Species.Localizations.PreferredOrFirst(t =>
        Settings.Language.EqualsIc(t.Resource?.Name))?.Name ?? Pokemon.Name;

    /// <summary>
    /// The characters description (defaults to the species flavor texts)
    /// </summary>
    [JsonPropertyName("description")]
    public string Description
    {
        get => _description
            ??= Species.FlavorTexts.PreferredOrFirst(t => Settings.Language.EqualsIc(t.Resource?.Name))?.Value
            ?? Species.Genera.PreferredOrFirst(t => Settings.Language.EqualsIc(t.Resource?.Name))?.Value
            ?? Name;
        set => _description = value;
    }

    /// <summary>
    /// The level of the Pokémon (clamped between 1 and 100)
    /// </summary>
    [JsonPropertyName("level")]
    public int Level
    {
        get => _level;
        set
        {
            _level = Math.Clamp(value, 1, 100);
            RecomputeStats();
        }
    }

    /// <summary>
    /// Gets the cached collection of Pokémon data.
    /// </summary>
    [JsonPropertyName("data")]
    public required PokemonCache Data { get; set; }

    /// <summary>
    /// The settings for the translation
    /// </summary>
    [JsonPropertyName("settings")]
    public required TranslateSettings Settings { get; set; }

    /// <summary>
    /// The Pokémon's EVs
    /// </summary>
    [JsonPropertyName("evs")]
    public required PokemonEVs EVs
    {
        get => _evs ?? throw new ArgumentNullException(nameof(EVs));
        set
        {
            _evs = value;
            RecomputeStats();
        }
    }

    /// <summary>
    /// The Pokémon's IVs
    /// </summary>
    [JsonPropertyName("ivs")]
    public required PokemonIVs IVs
    {
        get => _ivs ?? throw new ArgumentNullException(nameof(IVs));
        set
        {
            _ivs = value;
            RecomputeStats();
        }
    }

    /// <summary>
    /// The Pokémon's nature
    /// </summary>
    [JsonPropertyName("nature")]
    public required PkNature Nature
    {
        get => _nature ?? throw new ArgumentNullException(nameof(Nature));
        set
        {
            _nature = value;
            RecomputeStats();
        }
    }

    /// <summary>
    /// The Pokémon's base stats
    /// </summary>
    [JsonPropertyName("baseStats")]
    public PokemonBaseStats BaseStats
    {
        get => _baseStats ??= new(Pokemon);
        internal set => _baseStats = value;
    }

    /// <summary>
    /// The Pokémon's calculated stats
    /// </summary>
    [JsonPropertyName("stats")]
    public PokemonStats Stats
    {
        get => _stats ??= CalculateStats();
        internal set => _stats = value;
    }

    /// <summary>
    /// The Pokémon's stats scaled to D&D standards
    /// </summary>
    [JsonPropertyName("dndStats")]
    public DndStats DndStats
    {
        get => GetDndStats();
        set => _dndStats = value;
    }

    /// <summary>
    /// The Pokémon's base data
    /// </summary>
    [JsonIgnore]
    public PkPokemon Pokemon
    {
        get => Data.Pokemon;
        set
        {
            Data.Pokemon = value;
            RecomputeStats();
        }
    }

    /// <summary>
    /// The Pokémon's species
    /// </summary>
    [JsonIgnore]
    public PkPokemonSpecies Species => Data.Species;

    /// <summary>
    /// The Pokémon's growth rate
    /// </summary>
    [JsonIgnore]
    public PkGrowthRate Rate => Data.GrowthRate;

    /// <summary>
    /// Recomputes the Pokemon's stats from the species base stats
    /// </summary>
    public void RecomputeStats()
    {
        _baseStats = null;
        _dndStats = null;
        _stats = null;
    }

    internal PokemonStats CalculateStats()
    {
        var stats = new StatCalculator
        {
            EVs = EVs,
            IVs = IVs,
            NatureModifiers = StatCalculator.Nature(Nature),
            Stats = BaseStats
        };
        var result = stats.Calculate(Level, out var errors);
        var error = string.Join("\r\n", errors);
        if (!string.IsNullOrEmpty(error))
            throw new ArgumentException($"Stats could not be computed:\r\n{error}", nameof(Stats));
        return result;
    }

    internal DndStats GetDndStats()
    {
        _dndStats ??= new(Stats, Settings);
        _dndStats._stats = Stats;
        _dndStats._settings = Settings;
        return _dndStats;
    }
}
