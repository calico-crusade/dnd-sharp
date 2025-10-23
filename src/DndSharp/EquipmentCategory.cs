using DndSharp.Abstraction;

namespace DndSharp;

/// <summary>
/// Represents a category of equipment, such as "Weapons", "Armor", or "Adventuring Gear".
/// </summary>
/// <param name="Name">The name of the category</param>
public record class EquipmentCategory(string Name) 
    : NamedEntity<EquipmentCategory>(Name);
