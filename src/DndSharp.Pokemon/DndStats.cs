namespace DndSharp.Pokemon;

public class DndStats
{
    private int? _str, _dex, _con, _int, _wis, _chr;
    internal IPokemonStats? _stats = null;
    internal TranslateSettings? _settings = null;

    [JsonConstructor]
    internal DndStats() { }

    public DndStats(IPokemonStats stats, TranslateSettings settings)
    {
        _stats = stats;
        _settings = settings;
    }

    /// <summary>
    /// The pokemon's strength stat (based on it's attack)
    /// </summary>
    [JsonPropertyName("str")]
    public int Strength 
    {  
        get => _str ??= Scale(_stats?.Attack); 
        set => _str = value; 
    }

    /// <summary>
    /// The pokemon's dexterity (based on it's speed)
    /// </summary>
    [JsonPropertyName("dex")]
    public int Dexterity
    {
        get => _dex ??= Scale(_stats?.Speed);
        set => _dex = value;
    }

    /// <summary>
    /// The pokemon's constitution (based on it's HP and defense)
    /// </summary>
    [JsonPropertyName("con")]
    public int Constitution
    {
        get => _con ??= ScaleHp();
        set => _con = value;
    }

    /// <summary>
    /// The pokemon's intelligence (based on it's special attack)
    /// </summary>
    [JsonPropertyName("int")]
    public int Intelligence
    {
        get => _int ??= Scale(_stats?.SpecialAttack);
        set => _int = value;
    }

    /// <summary>
    /// The pokemon's wisdom (based on it's special defense)
    /// </summary>
    [JsonPropertyName("wis")]
    public int Wisdom
    {
        get => _wis ??= Scale(_stats?.SpecialDefense);
        set => _wis = value;
    }

    /// <summary>
    /// The pokemon's charisma (based on it's special attack and special defense)
    /// </summary>
    [JsonPropertyName("chr")]
    public int Charisma
    {
        get => _chr ??= ScaleChr();
        set => _chr = value;
    }

    /// <summary>
    /// Recomputes the pokemon's stats
    /// </summary>
    public void Recompute()
    {
        _str = _dex = _con = _int = _wis = _chr = null;
    }

    /// <summary>
    /// Scale the given pokemon stat by the settings
    /// </summary>
    /// <param name="stat">The pokemon stat (HP, Speed, etc)</param>
    /// <returns>The scaled value</returns>
    internal int Scale(double? stat)
    {
        if (stat is null || _stats is null)
            return 0;

        var settings = _settings ?? new();
        var slope = (settings.HighScore - settings.LowScore) / 
            (settings.HighStat - settings.LowStat);
        var raw = settings.LowScore + slope * (stat.Value - settings.LowStat);
        var rounded = Math.Round(raw, MidpointRounding.AwayFromZero);
        return (int)Math.Clamp(rounded, settings.MinScore, settings.MaxScore);
    }

    /// <summary>
    /// Scale the pokemon's HP by the settings
    /// </summary>
    /// <returns>The scaled value</returns>
    internal int ScaleHp()
    {
        if (_stats is null) return 0;

        var settings = _settings ?? new();
        var stat = (settings.HpWeight * _stats.Hp) + (settings.DefWeight * _stats.Defense);
        return Scale(stat);
    }

    /// <summary>
    /// Scale the pokemon's Charisma by the settings
    /// </summary>
    /// <returns>The scaled value</returns>
    internal int ScaleChr()
    {
        if (_stats is null) return 0;

        var settings = _settings ?? new();
        double[] stats = [_stats.Attack, _stats.Defense, _stats.SpecialAttack, _stats.SpecialDefense, _stats.Speed];
        var ordered = stats.Select(t => Scale(t)).OrderByDescending(s => s);
        double[] scales = [0.5, 0.45, 0.25];

        int i = -1;
        var total = ordered.Take(scales.Length).Sum(t => t / settings.HighScore * scales[++i]);
        return (int)Math.Clamp(total * settings.HighScore, settings.MinScore, settings.MaxScore);
    }
}
