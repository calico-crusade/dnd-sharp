namespace DndSharp;

using Abstraction;

/// <summary>
/// Represents a skill
/// </summary>
/// <param name="Name">The name of the skill</param>
/// <param name="Description">The description of the skill</param>
/// <param name="AbilityScore">The type of skill, indicating how it affects a character.</param>
public record class Skill(
    string Name,
    string Description,
    [property: JsonPropertyName("score")] AbilityScore AbilityScore) 
    : DescribedEntity<Skill>(Name, Description);
