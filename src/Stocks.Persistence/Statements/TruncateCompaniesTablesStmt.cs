namespace Stocks.Persistence;

internal sealed class TruncateCompaniesTablesStmt : NonQueryBatchedDbStmtBase
{
    private const string TruncateCompaniesSql = "TRUNCATE TABLE companies";
    private const string TruncateCompanyNamesSql = "TRUNCATE TABLE company_names";

    public TruncateCompaniesTablesStmt() : base(nameof(TruncateCompaniesTablesStmt))
    {
        AddCommandToBatch(TruncateCompaniesSql, []);
        AddCommandToBatch(TruncateCompanyNamesSql, []);
    }
}
