namespace Stocks.Persistence.Database.Statements;

/// <summary>
/// Empties the companies tables before the full CIK list is reloaded. company_tickers has
/// a foreign key to companies, so it is truncated in the same statement (a lone TRUNCATE of
/// companies fails). Its rows point at company ids the reload regenerates; the ticker
/// mappings import reloads it.
/// </summary>
internal sealed class TruncateCompaniesTablesStmt : NonQueryBatchedDbStmtBase {
    private const string TruncateSql = "TRUNCATE TABLE companies, company_names, submissions, company_tickers";

    public TruncateCompaniesTablesStmt() : base(nameof(TruncateCompaniesTablesStmt)) {
        AddCommandToBatch(TruncateSql, []);
    }
}
