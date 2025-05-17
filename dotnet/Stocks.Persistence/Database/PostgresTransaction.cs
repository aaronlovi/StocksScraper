using System;
using Npgsql;

namespace Stocks.Persistence.Database;

public sealed class PostgresTransaction(NpgsqlConnection _connection, NpgsqlTransaction _transaction, SemaphoreLocker _limiter) : IDisposable {
    public NpgsqlConnection Connection => _connection;
    public NpgsqlTransaction Transaction => _transaction;
    public SemaphoreLocker Limiter => _limiter;

    public void Commit() => Transaction.Commit();

    public void Rollback() => Transaction.Rollback();

    public void Dispose() {
        Transaction.Dispose();
        Connection.Dispose();
        Limiter.Dispose();
    }
}
