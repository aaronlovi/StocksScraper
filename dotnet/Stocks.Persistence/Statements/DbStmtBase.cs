using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

namespace Stocks.Persistence.Statements;

internal static class DbUtils {
    internal static NpgsqlParameter CreateNullableDateTimeParam(string paramName, DateTimeOffset? nullableDate) {
        var param = new NpgsqlParameter(paramName, NpgsqlDbType.TimestampTz) {
            Value = nullableDate?.UtcDateTime ?? (object)DBNull.Value
        };
        return param;
    }
}

internal abstract class DbStmtBase {
    protected abstract IReadOnlyCollection<NpgsqlParameter> GetBoundParameters();
}

/// <summary>
/// Represents the base class for all query database statements executed against a
/// PostgreSQL database using Npgsql. This abstract class provides the framework for
/// executing SQL queries, preparing commands, and processing result sets.
/// </summary>
/// <remarks>
/// Derived classes should implement the <see cref="ProcessCurrentRow"/> method to
/// define how individual rows in the result set are processed.
/// Additionally, they may override the <see cref="BeforeRowProcessing"/> method to
/// perform any necessary setup or initialization before row processing begins.
/// This class handles common tasks such as command preparation, parameter binding,
/// and execution of the reader loop, allowing derived classes to focus on the
/// specifics of their respective queries.
/// </remarks>
internal abstract class QueryDbStmtBase(string _sql, string _className) : DbStmtBase, IPostgresStatement {
    /// <summary>
    /// Executes the SQL query defined in the derived class against the provided NpgsqlConnection.
    /// </summary>
    /// <returns>
    /// A Task that represents the asynchronous operation, containing the result of the query
    /// execution as a <see cref="DbStmtResult"/>.
    /// </returns>
    /// <remarks>
    /// This method prepares the SQL command, binds any parameters required for the query, and
    /// executes the command asynchronously. It iterates over the result set, processing each 
    /// row using the <see cref="ProcessCurrentRow"/> method implemented in the derived class.
    /// Before processing the rows, it calls <see cref="BeforeRowProcessing"/> to allow for
    /// any necessary setup.
    /// This method handles exceptions by clearing any results and returning a failure result,
    /// ensuring that the caller can gracefully handle errors.
    /// </remarks>
    public async Task<DbStmtResult> Execute(NpgsqlConnection conn, CancellationToken ct) {
        ClearResults();

        try {
            using var cmd = new NpgsqlCommand(_sql, conn);
            foreach (NpgsqlParameter boundParam in GetBoundParameters())
                _ = cmd.Parameters.Add(boundParam);
            await cmd.PrepareAsync(ct);
            using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(ct);

            BeforeRowProcessing(reader);

            int numRows = 0;
            while (await reader.ReadAsync(ct)) {
                ++numRows;
                if (!ProcessCurrentRow(reader))
                    break;
            }

            AfterLastRowProcessing();

            return DbStmtResult.StatementSuccess(numRows);
        } catch (Exception ex) {
            ClearResults();
            string errMsg = $"{_className} failed - {ex.Message}";
            return DbStmtResult.StatementFailure(errMsg);
        }
    }

    /// <summary>
    /// Clears any results or state from a previous query execution.
    /// </summary>
    /// <remarks>
    /// This method is designed to reset the state of the derived query statement class,
    /// ensuring that it is ready for a new execution cycle. It should be called before
    /// executing a new query to prevent data from previous executions from affecting the
    /// results of the current execution. Derived classes should override this method to
    /// clear specific results or state information related to their query.
    /// </remarks>
    protected abstract void ClearResults();

    /// <summary>
    /// Performs any necessary setup or initialization before processing the rows of the
    /// query result set.
    /// </summary>
    /// <remarks>
    /// This method is called once before the row processing loop begins in the
    /// <see cref="Execute"/> method.
    /// It provides a hook for derived classes to perform any setup tasks that are necessary
    /// before processing individual rows.
    /// Common uses include caching column ordinals for efficient access during row processing
    /// or initializing data structures to hold the results.
    /// Derived classes overriding this method should ensure to call the base method if it
    /// contains implementation.
    /// </remarks>
    protected virtual void BeforeRowProcessing(NpgsqlDataReader reader) { }

    protected virtual void AfterLastRowProcessing() { }

    /// <summary>
    /// Processes the current row in the query result set.
    /// </summary>
    /// <returns>
    /// A boolean value indicating whether to continue processing rows.
    /// Returning false will stop the row processing loop.
    /// </returns>
    /// <remarks>
    /// This method is called for each row in the result set of the query execution.
    /// Derived classes must implement this method to define how individual rows
    /// should be processed.
    /// The method provides direct access to the current row through the
    /// <paramref name="reader"/> parameter, allowing derived classes to read the
    /// necessary data from the row.
    /// Implementations can return false to prematurely stop the processing of rows,
    /// which can be useful in scenarios where not all rows need to be processed or
    /// certain conditions are met.
    /// </remarks>
    protected abstract bool ProcessCurrentRow(NpgsqlDataReader reader);
}

internal abstract class NonQueryDbStmtBase(string _sql, string _className) : DbStmtBase, IPostgresStatement {
    public async Task<DbStmtResult> Execute(NpgsqlConnection conn, CancellationToken ct) {
        try {
            using var cmd = new NpgsqlCommand(_sql, conn);
            foreach (NpgsqlParameter boundParam in GetBoundParameters())
                _ = cmd.Parameters.Add(boundParam);
            await cmd.PrepareAsync(ct);
            int numRows = await cmd.ExecuteNonQueryAsync(ct);
            return DbStmtResult.StatementSuccess(numRows);
        } catch (Exception ex) {
            string errMsg = $"{_className} failed - {ex.Message}";
            return DbStmtResult.StatementFailure(errMsg);
        }
    }
}

internal abstract class NonQueryBatchedDbStmtBase(string _className) : IPostgresStatement {
    private readonly List<NpgsqlBatchCommand> _commands = [];

    public async Task<DbStmtResult> Execute(NpgsqlConnection conn, CancellationToken ct) {
        try {
            using var batch = new NpgsqlBatch(conn);
            foreach (NpgsqlBatchCommand cmd in _commands)
                batch.BatchCommands.Add(cmd);
            int numRows = await batch.ExecuteNonQueryAsync(ct);
            return DbStmtResult.StatementSuccess(numRows);
        } catch (PostgresException ex) {
            string errMsg = $"{_className} failed - {ex.Message}";
            DbStmtFailureReason failureReason = ex.SqlState == "23505" ? DbStmtFailureReason.Duplicate : DbStmtFailureReason.Other;
            return DbStmtResult.StatementFailure(errMsg, failureReason);
        } catch (Exception ex) {
            string errMsg = $"{_className} failed - {ex.Message}";
            return DbStmtResult.StatementFailure(errMsg);
        }
    }

    protected void AddCommandToBatch(string sql, IReadOnlyCollection<NpgsqlParameter> boundParams) {
        var cmd = new NpgsqlBatchCommand(sql);
        foreach (NpgsqlParameter boundParam in boundParams)
            _ = cmd.Parameters.Add(boundParam);
        _commands.Add(cmd);
    }
}

internal abstract class BulkInsertDbStmtBase<T>(string _className, IReadOnlyCollection<T> _items)
    : IPostgresStatement
    where T : class {
    protected abstract string GetCopyCommand();
    protected abstract Task WriteItemAsync(NpgsqlBinaryImporter writer, T item);

    public async Task<DbStmtResult> Execute(NpgsqlConnection conn, CancellationToken ct) {
        T? failedItem = default;

        try {
            using NpgsqlBinaryImporter writer = conn.BeginBinaryImport(GetCopyCommand());

            foreach (T item in _items) {
                failedItem = item;
                await writer.StartRowAsync(ct);
                await WriteItemAsync(writer, item);
            }

            _ = await writer.CompleteAsync(ct);
            return DbStmtResult.StatementSuccess(_items.Count);
        } catch (PostgresException ex) {
            string errMsg = $"{_className} failed - {ex.Message}";
            DbStmtFailureReason failureReason = ex.SqlState == "23505"
                ? DbStmtFailureReason.Duplicate
                : DbStmtFailureReason.Other;
            return DbStmtResult.StatementFailure(errMsg, failureReason);
        } catch (Exception ex) {
            string failedItemStr = failedItem?.ToString() ?? "NULL";
            string errMsg = $"{_className} failed - {ex.Message}. Item: {failedItemStr}";
            return DbStmtResult.StatementFailure(errMsg);
        }
    }
}
