using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

namespace Stocks.Persistence.Database;

internal static class Extensions {
    internal static void WriteNullable<T>(this NpgsqlBinaryImporter writer, T? obj, NpgsqlDbType type)
        where T : class {
        if (obj is null)
            writer.WriteNull();
        else
            writer.Write(obj, type);
    }

    internal static void WriteNullable<T>(this NpgsqlBinaryImporter writer, T? obj, NpgsqlDbType type)
        where T : struct {
        if (obj.HasValue)
            writer.Write(obj.Value, type);
        else
            writer.WriteNull();
    }

    internal static Task WriteNullableAsync<T>(this NpgsqlBinaryImporter writer, T? obj, NpgsqlDbType type)
        where T : class
        => obj is null ? writer.WriteNullAsync() : writer.WriteAsync(obj, type);

    internal static Task WriteNullableAsync<T>(this NpgsqlBinaryImporter writer, T? obj, NpgsqlDbType type)
        where T : struct
        => obj.HasValue ? writer.WriteAsync(obj.Value, type) : writer.WriteNullAsync();

    internal static T? GetNullableValueType<T>(this NpgsqlDataReader reader, int ordinal)
        where T : struct
        => reader.IsDBNull(ordinal) ? default : reader.GetFieldValue<T>(ordinal);

    internal static T? GetNullableRefType<T>(this NpgsqlDataReader reader, int ordinal)
        where T : class
        => reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<T>(ordinal);

    internal static Task<T?> GetNullableValueTypeAsync<T>(this NpgsqlDataReader reader, int ordinal)
        where T : struct
        => reader.IsDBNull(ordinal) ? Task.FromResult<T?>(default) : reader.GetFieldValueAsync<T?>(ordinal);

    internal static Task<T?> GetNullableRefTypeAsync<T>(this NpgsqlDataReader reader, int ordinal)
        where T : class
        => reader.IsDBNull(ordinal) ? Task.FromResult<T?>(null) : reader.GetFieldValueAsync<T?>(ordinal);
}
