using System;
using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence.Statements;

internal sealed class GetPagedCompaniesByDataSourceStmt : QueryDbStmtBase
{
    private const string sql = @"
WITH TotalCount AS (
    SELECT COUNT(*) AS total
    FROM companies
    WHERE data_source = @data_source
)
SELECT company_id, cik, data_source, total
FROM companies, TotalCount
WHERE data_source = @data_source
LIMIT @limit OFFSET @offset";

    // Inputs
    private readonly string _dataSource;
    private readonly PaginationRequest _pagination;

    // Outputs
    private readonly List<Company> _companies;
    private PaginationResponse _paginationResponse;

    private static int _companyIdIndex = -1;
    private static int _cikIndex = -1;
    private static int _dataSourceIndex = -1;
    private static int _totalIndex = -1;

    public GetPagedCompaniesByDataSourceStmt(string dataSource, PaginationRequest pagination)
        : base(sql, nameof(GetPagedCompaniesByDataSourceStmt))
    {
        _dataSource = dataSource;
        _pagination = pagination;
        _companies = [];
        _paginationResponse = PaginationResponse.Empty;
    }

    public IReadOnlyCollection<Company> Companies => _companies;
    public PaginationResponse PaginationResponse => _paginationResponse;

    public PagedCompanies GetPagedCompanies() => new(_companies, _paginationResponse);

    protected override void BeforeRowProcessing(NpgsqlDataReader reader)
    {
        base.BeforeRowProcessing(reader);

        if (_companyIdIndex != -1) return;

        _companyIdIndex = reader.GetOrdinal("company_id");
        _cikIndex = reader.GetOrdinal("cik");
        _dataSourceIndex = reader.GetOrdinal("data_source");
        _totalIndex = reader.GetOrdinal("total");
    }

    protected override void ClearResults()
    {
        _companies.Clear();
        _paginationResponse = PaginationResponse.Empty;
    }

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() => [
        new NpgsqlParameter<string>("data_source", _dataSource),
        new NpgsqlParameter<int>("limit", (int)_pagination.PageSize) { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer },
        new NpgsqlParameter<int>("offset", (int)((_pagination.PageNumber - 1) * _pagination.PageSize)) { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer } ];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader)
    {
        if (_companies.Count == 0)
        {
            uint totalItems = (uint)reader.GetInt64(_totalIndex);
            // Note: _pagination.PageSize is guaranteed non-zero by construction of PaginationResponse
            uint totalPages = (uint)Math.Ceiling(totalItems / (double)_pagination.PageSize);
            _paginationResponse = new PaginationResponse(_pagination.PageNumber, totalItems, totalPages);
        }
        var company = new Company(
            (ulong)reader.GetInt64(_companyIdIndex),
            (ulong)reader.GetInt64(_cikIndex),
            reader.GetString(_dataSourceIndex));
        _companies.Add(company);
        return true;
    }
}
