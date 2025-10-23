namespace DndSharp.Abstraction;

/// <summary>
/// A component of a character
/// </summary>
public interface ICharacterComponent
{
    /// <summary>
    /// The name of the component
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The description of the component
    /// </summary>
    string Description { get; }
}
