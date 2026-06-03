using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Draw.Model.Styling;

namespace Draw.Model.Serialization;

/// <summary>Serializes <see cref="ArgbColor"/> as a compact <c>#AARRGGBB</c> string.</summary>
public sealed class ArgbColorJsonConverter : JsonConverter<ArgbColor>
{
    public override ArgbColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? hex = reader.GetString();
        if (ArgbColor.TryParse(hex, out ArgbColor color))
        {
            return color;
        }

        throw new JsonException($"'{hex}' is not a valid color.");
    }

    public override void Write(Utf8JsonWriter writer, ArgbColor value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToHex());
}
