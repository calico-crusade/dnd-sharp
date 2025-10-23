namespace DndSharp.Dice.NumberGenerator;

/// <summary>
/// Represents a service that can be used to generate random numbers
/// </summary>
public interface INumberGenerator
{
    /// <summary>
    /// Generates a random number between the specified minimum and maximum values.
    /// </summary>
    /// <param name="min">The minimum value</param>
    /// <param name="max">The inclusive maximum value</param>
    /// <param name="token">Cancellation token for the request</param>
    /// <returns>The randomly generated number</returns>
    Task<int> Generate(int min, int max, CancellationToken token = default);

    /// <summary>
    /// Generates a random number between 1 and the specified maximum value.
    /// </summary>
    /// <param name="max">The inclusive maximum value</param>
    /// <param name="token">Cancellation token for the request</param>
    /// <returns>The randomly generated number</returns>
    Task<int> Generate(int max, CancellationToken token = default) => Generate(1, max, token);
}
