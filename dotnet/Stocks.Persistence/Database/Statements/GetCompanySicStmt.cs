using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence.Database.Statements;

/// <summary>One company's SIC code; <see cref="Result"/> stays null when the company has none.</summary>
internal sealed class GetCompanySicStmt : QueryDbStmtBase {
    private const string sql = @"
SELECT company_id, sic_code, sic_description
FROM companies
WHERE company_id = @company_id AND sic_code IS NOT NULL;
";

    private readonly ulong _companyId;

    private int _companyIdIndex = -1;
    private int _sicCodeIndex = -1;
    private int _sicDescriptionIndex = -1;

    public GetCompanySicStmt(ulong companyId) : base(sql, nameof(GetCompanySicStmt)) {
        _companyId = companyId;
    }

    public CompanySic? Result { get; private set; }

    protected override void BeforeRowProcessing(NpgsqlDataReader reader) {
        base.BeforeRowProcessing(reader);
        _companyIdIndex = reader.GetOrdinal("company_id");
        _sicCodeIndex = reader.GetOrdinal("sic_code");
        _sicDescriptionIndex = reader.GetOrdinal("sic_description");
    }

    protected override void ClearResults() => Result = null;

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [new NpgsqlParameter<long>("company_id", (long)_companyId)];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader) {
        Result = new CompanySic(
            (ulong)reader.GetInt64(_companyIdIndex),
            reader.GetInt32(_sicCodeIndex),
            reader.IsDBNull(_sicDescriptionIndex) ? null : reader.GetString(_sicDescriptionIndex));
        return false;
    }
}
