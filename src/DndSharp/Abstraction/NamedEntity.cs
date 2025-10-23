namespace DndSharp.Abstraction;

/// <summary>
/// Represents an entity with a name
/// </summary>
/// <param name="Name">The name of the entity</param>
public abstract record class NamedEntity<TSelf>(
    [property: JsonPropertyName("name")] string Name) : IEquatable<TSelf>
    where TSelf : NamedEntity<TSelf>
{
    [JsonIgnore]
    internal virtual string[] ItemsToCheck => [Name];

    internal static bool EqualityCheck(NamedEntity<TSelf>? current, string? toCheck)
    {
        if (current is null && string.IsNullOrEmpty(toCheck)) return true;

        if (current is null) return false;

        return current.ItemsToCheck.Any(t => 
            t.Equals(toCheck, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <inheritdoc />
    public bool Equals(TSelf? other) => EqualityCheck(this, other?.Name);

    /// <inheritdoc />
    public bool Equals(string? other) => EqualityCheck(this, other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Name.GetHashCode(StringComparison.InvariantCultureIgnoreCase);
    }

    /// <inheritdoc />
    public override string ToString() => Name;

    /// <inheritdoc />
    public static bool operator ==(NamedEntity<TSelf>? left, string? right)
    {
        return EqualityCheck(left, right);
    }

    /// <inheritdoc />
    public static bool operator ==(string? left, NamedEntity<TSelf>? right)
    {
        return EqualityCheck(right, left);
    }

    /// <inheritdoc />
    public static bool operator !=(NamedEntity<TSelf>? left, string? right) => !EqualityCheck(left, right);

    /// <inheritdoc />
    public static bool operator !=(string? left, NamedEntity<TSelf>? right) => !EqualityCheck(right, left);
}
