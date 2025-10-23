namespace DndSharp;

using Abstraction;

/// <summary>
/// A property of a weapon, such as "finesse" or "heavy".
/// </summary>
/// <param name="Name">The name of the property</param>
/// <param name="Description">The description of the property</param>
public record class WeaponProperty(
    string Name,
    string Description) 
    : DescribedEntity<WeaponProperty>(Name, Description);
