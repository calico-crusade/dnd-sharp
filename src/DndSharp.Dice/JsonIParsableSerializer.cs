namespace DndSharp;

/// <summary>
/// A JSON converter that uses the <see cref="IParsable{T}"/> interface to parse and serialize objects.
/// </summary>
/// <typeparam name="T">The type of object</typeparam>
public class JsonIParsableSerializer<T> : JsonConverter<T>
    where T : IParsable<T>
{
    /// <inheritdoc />
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"[{nameof(JsonIParsableSerializer<T>)}] Expected string, got {reader.TokenType} @ {reader.TokenStartIndex}");

        var value = reader.GetString() ?? string.Empty;
        return T.Parse(value, null);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
