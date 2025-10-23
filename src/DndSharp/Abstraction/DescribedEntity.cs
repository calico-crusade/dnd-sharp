namespace DndSharp.Abstraction;

/// <summary>
/// Represents an entity with a name and description.
/// </summary>
/// <param name="Name">The name of the entity</param>
/// <param name="Description">The description of the entity</param>
public abstract record class DescribedEntity<TSelf>(
    string Name,
    [property: JsonPropertyName("description")] string Description) 
    : NamedEntity<TSelf>(Name), ICharacterComponent
    where TSelf : DescribedEntity<TSelf>;
