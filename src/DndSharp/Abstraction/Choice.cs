namespace DndSharp.Abstraction;

/// <summary>
/// Represents a choice that is made in the character creation process or during gameplay.
/// </summary>
/// <typeparam name="T">The type of choice to make</typeparam>
/// <param name="Choices">The choices that can be made</param>
/// <param name="Choose">How many choices that can be made</param>
public record class Choice<T>(
    [property: JsonPropertyName("choose")] int Choose,
    [property: JsonPropertyName("choices")] T[] Choices);

/// <summary>
/// Represents a choice that is made in the character creation process or during gameplay.
/// </summary>
public static class Choice
{
    /// <summary>
    /// Creates a choice that allows the user to select a specific number of items from a list of choices.
    /// </summary>
    /// <typeparam name="T">The type of choice to make</typeparam>
    /// <param name="count">The number of choices to choose</param>
    /// <param name="items">The items to choose from</param>
    /// <returns>The choice object</returns>
    public static Choice<T> Choose<T>(int count, params T[] items) => new(count, items);

    /// <summary>
    /// Creates a choice that allows the user to select one item from a list of choices.
    /// </summary>
    /// <typeparam name="T">The type of choice to make</typeparam>
    /// <param name="items">The items to choose from</param>
    /// <returns>The choice object</returns>
    public static Choice<T> One<T>(params T[] items) => Choose(1, items);

    /// <summary>
    /// Creates a choice that allows the user to select two items from a list of choices.
    /// </summary>
    /// <typeparam name="T">The type of choice to make</typeparam>
    /// <param name="items">The items to choose from</param>
    /// <returns>The choice object</returns>
    public static Choice<T> Two<T>(params T[] items) => Choose(2, items);

    /// <summary>
    /// Creates a choice that allows the user to select three items from a list of choices.
    /// </summary>
    /// <typeparam name="T">The type of choice to make</typeparam>
    /// <param name="items">The items to choose from</param>
    /// <returns>The choice object</returns>
    public static Choice<T> Three<T>(params T[] items) => Choose(3, items);
}
