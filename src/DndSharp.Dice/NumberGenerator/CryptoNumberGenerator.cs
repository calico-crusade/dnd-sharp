using System.Security.Cryptography;

namespace DndSharp.Dice.NumberGenerator;

/// <summary>
/// A service that generates numbers based on a cryptographically secure random number generator.
/// </summary>
public class CryptoNumberGenerator : INumberGenerator
{
    /// <inheritdoc />
    public Task<int> Generate(int min, int max, CancellationToken _ = default)
    {
        var result = RandomNumberGenerator.GetInt32(min, max + 1);
        return Task.FromResult(result);
    }
}
