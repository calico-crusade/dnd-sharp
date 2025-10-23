namespace DndSharp;

using Abstraction;

/// <summary>
/// Represents the equipment a character possesses, such as weapons, armor, and other items.
/// </summary>
/// <param name="Name">The name of the equipment</param>
/// <param name="Description">The description of the equipment</param>
/// <param name="Categories">All of the categories of equipment this entity belongs to.</param>
public record class Equipment(
    string Name,
    string Description,
    [property: JsonPropertyName("categories")]
    EquipmentCategory[] Categories) 
    : DescribedEntity<Equipment>(Name, Description);
