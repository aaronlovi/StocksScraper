using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stocks.Shared.JsonUtils;

public class StringToUlongConverter : JsonConverter<ulong> {
    public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ulong.Parse(reader.GetString() ?? throw new JsonException("Invalid value for ulong conversion."));

    public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
