namespace DndSharp.Abstraction;

/// <summary>
/// Represents a static resource for the engine
/// </summary>
public interface IStaticResource<T> where T : class
{
    /// <summary>
    /// All of the instances of the resource
    /// </summary>
    static abstract T[] All { get; }
}
