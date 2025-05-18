using System.Collections.Generic;
using Npgsql;

namespace Stocks.Persistence.Database.Statements;

internal sealed class DropAllTablesStmt : NonQueryDbStmtBase {
    private const string sql = @"
DROP TABLE IF EXISTS changelog;
DROP TABLE IF EXISTS companies;
DROP TABLE IF EXISTS company_names;
DROP TABLE IF EXISTS data_point_units;
DROP TABLE IF EXISTS data_points;
DROP TABLE IF EXISTS filing_categories;
DROP TABLE IF EXISTS filing_types;
DROP TABLE IF EXISTS generator;
DROP TABLE IF EXISTS submissions;
DROP TABLE IF EXISTS taxonomy_period_types;
DROP TABLE IF EXISTS taxonomy_balance_types;
DROP TABLE IF EXISTS taxonomy_types;
DROP TABLE IF EXISTS taxonomy_concepts;
DROP TABLE IF EXISTS taxonomy_presentation;
";

    public DropAllTablesStmt() : base(sql, nameof(DropAllTablesStmt)) { }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [];
}
