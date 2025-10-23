namespace DndSharp.Abstraction;

/// <summary>
/// Represents a quantity of an item, such as a potion or piece of equipment.
/// </summary>
/// <typeparam name="T">The type of item</typeparam>
/// <param name="Amount">The amount of the item that the character possesses.</param>
/// <param name="Item">The item that this quantity represents, such as a potion or piece of equipment.</param>
public record class Quantity<T>(
    [property: JsonPropertyName("item")] T Item, 
    [property: JsonPropertyName("amount")] int Amount);

/// <summary>
/// Represents a quantity of an item, such as a potion or piece of equipment.
/// </summary>
public static class Quantity
{
    /// <summary>
    /// Creates a quantity of an item, such as a potion or piece of equipment.
    /// </summary>
    /// <typeparam name="T">The type of item</typeparam>
    /// <param name="item">The item that this quantity represents, such as a potion or piece of equipment.</param>
    /// <param name="amount">The amount of the item that the character possesses.</param>
    /// <returns>The quantity</returns>
    public static Quantity<T> Of<T>(T item, int amount) => new(item, amount);

    /// <summary>
    /// Creates a quantity of an item with a default amount of 1, such as a potion or piece of equipment.
    /// </summary>
    /// <typeparam name="T">The type of item</typeparam>
    /// <param name="item">The item that this quantity represents, such as a potion or piece of equipment.</param>
    /// <returns>The quantity</returns>
    public static Quantity<T> One<T>(T item) => Of(item, 1);

    /// <summary>
    /// Creates a quantity of an item with a default amount of 2, such as a potion or piece of equipment.
    /// </summary>
    /// <typeparam name="T">The type of item</typeparam>
    /// <param name="item">The item that this quantity represents, such as a potion or piece of equipment.</param>
    /// <returns>The quantity</returns>
    public static Quantity<T> Two<T>(T item) => Of(item, 2);

    /// <summary>
    /// Creates a quantity of an item with a default amount of 3, such as a potion or piece of equipment.
    /// </summary>
    /// <typeparam name="T">The type of item</typeparam>
    /// <param name="item">The item that this quantity represents, such as a potion or piece of equipment.</param>
    /// <returns>The quantity</returns>
    public static Quantity<T> Three<T>(T item) => Of(item, 3);
}
