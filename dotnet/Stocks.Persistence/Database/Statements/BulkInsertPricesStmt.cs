using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class BulkInsertPricesStmt : BulkInsertDbStmtBase<PriceRow> {
    public BulkInsertPricesStmt(IReadOnlyCollection<PriceRow> prices)
        : base(nameof(BulkInsertPricesStmt), prices) { }

    protected override string GetCopyCommand() => "COPY prices"
        + " (price_id, cik, ticker, exchange, stooq_symbol, price_date, open, high, low, close, volume)"
        + " FROM STDIN (FORMAT BINARY)";

    protected override async Task WriteItemAsync(NpgsqlBinaryImporter writer, PriceRow price) {
        await writer.WriteAsync((long)price.PriceId, NpgsqlDbType.Bigint);
        await writer.WriteAsync((long)price.Cik, NpgsqlDbType.Bigint);
        await writer.WriteAsync(price.Ticker, NpgsqlDbType.Varchar);
        await writer.WriteNullableAsync(price.Exchange, NpgsqlDbType.Varchar);
        await writer.WriteAsync(price.StooqSymbol, NpgsqlDbType.Varchar);
        await writer.WriteAsync(price.PriceDate.ToDateTime(TimeOnly.MinValue), NpgsqlDbType.Date);
        await writer.WriteAsync(price.Open, NpgsqlDbType.Numeric);
        await writer.WriteAsync(price.High, NpgsqlDbType.Numeric);
        await writer.WriteAsync(price.Low, NpgsqlDbType.Numeric);
        await writer.WriteAsync(price.Close, NpgsqlDbType.Numeric);
        await writer.WriteAsync(price.Volume, NpgsqlDbType.Bigint);
    }
}
