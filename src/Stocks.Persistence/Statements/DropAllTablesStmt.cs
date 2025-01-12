using System.Collections.Generic;
using Npgsql;
using Stocks.Persistence.Statements;

namespace Stocks.Persistence;

internal sealed class DropAllTablesStmt : NonQueryDbStmtBase
{
    private const string sql = @"
DROP TABLE changelog;
DROP TABLE companies;
DROP TABLE company_names;
DROP TABLE data_point_units;
DROP TABLE data_points;
DROP TABLE filing_categories;
DROP TABLE filing_types;
DROP TABLE generator;
DROP TABLE submissions;
";

    public DropAllTablesStmt() : base(sql, nameof(DropAllTablesStmt)) { }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];
}
