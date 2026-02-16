using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

internal sealed class GetTaxonomyTypeByNameVersionStmt : QueryDbStmtBase {
    private const string sql = @"
SELECT taxonomy_type_id, taxonomy_type_name, taxonomy_type_version
FROM taxonomy_types
WHERE taxonomy_type_name = @taxonomy_type_name
  AND taxonomy_type_version = @taxonomy_type_version;
";

    private readonly string _name;
    private readonly int _version;
    private TaxonomyTypeInfo? _taxonomyType;

    private static int _typeIdIndex = -1;
    private static int _typeNameIndex = -1;
    private static int _typeVersionIndex = -1;

    public GetTaxonomyTypeByNameVersionStmt(string name, int version)
        : base(sql, nameof(GetTaxonomyTypeByNameVersionStmt)) {
        _name = name;
        _version = version;
    }

    public TaxonomyTypeInfo? TaxonomyType => _taxonomyType;

    protected override void ClearResults() => _taxonomyType = null;

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [
            new NpgsqlParameter<string>("taxonomy_type_name", _name),
            new NpgsqlParameter<int>("taxonomy_type_version", _version)
        ];

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        if (_typeIdIndex != -1)
            return;
        _typeIdIndex = reader.GetOrdinal("taxonomy_type_id");
        _typeNameIndex = reader.GetOrdinal("taxonomy_type_name");
        _typeVersionIndex = reader.GetOrdinal("taxonomy_type_version");
    }

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        int id = reader.GetInt32(_typeIdIndex);
        string name = reader.GetString(_typeNameIndex);
        int version = reader.GetInt32(_typeVersionIndex);
        _taxonomyType = new TaxonomyTypeInfo(id, name, version);
        return false;
    }
}
