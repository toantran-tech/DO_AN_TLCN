using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WashGo.Core.Domain.Shared.Bases
{
    /// <summary>Reads a version token sent either as a JSON string or as a (legacy) JSON number; always kept as text.</summary>
    public sealed class OpaqueVersionConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => System.Text.Encoding.UTF8.GetString(reader.HasValueSequence ? System.Buffers.BuffersExtensions.ToArray(reader.ValueSequence) : reader.ValueSpan.ToArray()),
            _ => throw new JsonException("Version phải là chuỗi.")
        };

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value == null) writer.WriteNullValue(); else writer.WriteStringValue(value);
        }
    }
}
