namespace DndSharp.Pokemon;

public class TranslateSettings
{
    /// <summary>
    /// The language key to use for names and localizations
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    /// <summary>
    /// What is considered a low pokemon stat value
    /// </summary>
    [JsonPropertyName("lowStat")]
    public double LowStat { get; set; } = 90;

    /// <summary>
    /// What is considered a low D&D ability score value
    /// </summary>
    [JsonPropertyName("lowScore")]
    public double LowScore { get; set; } = 10;

    /// <summary>
    /// What is considered an elite pokemon stat value
    /// </summary>
    [JsonPropertyName("highStat")]
    public double HighStat { get; set; } = 210;

    /// <summary>
    /// What is considered an elite D&D ability score value
    /// </summary>
    [JsonPropertyName("highScore")]
    public double HighScore { get; set; } = 18;

    /// <summary>
    /// The minimum value for a D&D stat
    /// </summary>
    [JsonPropertyName("minScore")]
    public int MinScore { get; set; } = 3;

    /// <summary>
    /// The maximum value for a D&D stat
    /// </summary>
    [JsonPropertyName("maxScore")]
    public int MaxScore { get; set; } = 20;

    /// <summary>
    /// Weight to apply to the Pokemon's HP stat for the CON conversion
    /// </summary>
    [JsonPropertyName("hpWeight")]
    public double HpWeight { get; set; } = 0.60;

    /// <summary>
    /// Weight to apply to the Pokemon's Defense stat for the CON conversion
    /// </summary>
    [JsonPropertyName("defWeight")]
    public double DefWeight { get; set; } = 0.40;
}
