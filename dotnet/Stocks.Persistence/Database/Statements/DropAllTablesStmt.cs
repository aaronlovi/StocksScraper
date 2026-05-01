using System.Collections.Generic;
using Npgsql;

namespace Stocks.Persistence.Database.Statements;

internal sealed class DropAllTablesStmt : NonQueryDbStmtBase {
    private const string sql = @"
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN
        SELECT tablename
        FROM pg_tables
        WHERE schemaname = current_schema()
    LOOP
        EXECUTE 'DROP TABLE IF EXISTS ' || quote_ident(r.tablename) || ' CASCADE';
    END LOOP;
END $$;
";

    public DropAllTablesStmt() : base(sql, nameof(DropAllTablesStmt)) { }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];
}
