using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stocks.Shared.JsonUtils;

public class IntToBoolConverter : JsonConverter<bool> {
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int intValue)
            ? intValue == 1
            : throw new JsonException("Invalid value for boolean conversion.");
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value ? 1 : 0);
}
