namespace DndSharp;

using Abstraction;

/// <summary>
/// The various characteristics of a character
/// </summary>
/// <param name="Name">The name of the ability score, such as "Strength", "Dexterity", etc.</param>
/// <param name="Description">The description of the ability score, explaining its significance and effects in the game.</param>
/// <param name="BaseValue">The base value of the ability score, which is typically used as a starting point for calculations.</param>
/// <param name="MaxValue">The maximum value that this ability score can reach, which is often capped at 30</param>
public record class AbilityScore(
    string Name,
    string Description,
    [property: JsonPropertyName("baseValue")] int BaseValue = 10,
    [property: JsonPropertyName("maxValue")] int MaxValue = 30) 
    : DescribedEntity<AbilityScore>(Name, Description)
{
    /// <summary>
    /// The 3 letter abbreviation for the ability score, typically the first three letters of its name in uppercase.
    /// </summary>
    [JsonIgnore]
    public virtual string Abbreviation => Name[..3].ToUpperInvariant();

    [JsonIgnore]
    internal override string[] ItemsToCheck => [Name, Abbreviation];
}
