using System.Collections.Generic;
using Npgsql;

namespace Stocks.Persistence.Database.Statements;

internal sealed class InsertTaxonomyTypeStmt : NonQueryDbStmtBase {
    private const string sql = @"
INSERT INTO taxonomy_types (taxonomy_type_id, taxonomy_type_name, taxonomy_type_version)
VALUES (@taxonomy_type_id, @taxonomy_type_name, @taxonomy_type_version);
";

    private readonly int _id;
    private readonly string _name;
    private readonly int _version;

    public InsertTaxonomyTypeStmt(int id, string name, int version)
        : base(sql, nameof(InsertTaxonomyTypeStmt)) {
        _id = id;
        _name = name;
        _version = version;
    }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [
            new NpgsqlParameter<int>("taxonomy_type_id", _id),
            new NpgsqlParameter<string>("taxonomy_type_name", _name),
            new NpgsqlParameter<int>("taxonomy_type_version", _version)
        ];
}
