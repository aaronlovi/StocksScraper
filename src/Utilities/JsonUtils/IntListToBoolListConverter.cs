using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stocks.Shared.JsonUtils;

public class IntListToBoolListConverter : JsonConverter<List<bool>>
{
    public override List<bool> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var boolList = new List<bool>();

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected start of array");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int intValue))
                boolList.Add(intValue == 1);
            else
                throw new JsonException("Invalid value for boolean conversion");
        }

        return boolList;
    }

    public override void Write(Utf8JsonWriter writer, List<bool> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var boolValue in value)
            writer.WriteNumberValue(boolValue ? 1 : 0);

        writer.WriteEndArray();
    }
}
