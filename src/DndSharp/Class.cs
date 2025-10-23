namespace DndSharp;

using Abstraction;
using Dice;

/// <summary>
/// A character class, representing a specific role or archetype in the game.
/// </summary>
/// <param name="Name">The name of the class</param>
/// <param name="Description">The description of the class</param>
/// <param name="HitDie">The hit die used by this class, which determines the amount of hit points gained per level.</param>
/// <param name="ProficiencyChoices">The proficiencies the class can choose from</param>
/// <param name="Proficiencies">The proficiencies that the class has.</param>
/// <param name="SavingThrows">The saving throws that the class is proficient in.</param>
/// <param name="StartingEquipment">The equipment the class starts with, including weapons, armor, and other items.</param>
public record class Class(
    string Name,
    string Description,
    [property: JsonPropertyName("hitDie")] Die HitDie,
    [property: JsonPropertyName("proficiencyChoices")] Choice<Skill>[] ProficiencyChoices,
    [property: JsonPropertyName("proficiencies")] Skill[] Proficiencies,
    [property: JsonPropertyName("savingThrows")] AbilityScore[] SavingThrows,
    [property: JsonPropertyName("startingEquipment")] Quantity<Equipment>[] StartingEquipment) 
    : DescribedEntity<Class>(Name, Description);
