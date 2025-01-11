﻿#pragma warning disable IDE0290 // Use primary constructor

using System.Collections.Generic;
using Npgsql;
using Stocks.DataModels;

namespace Stocks.Persistence;

internal sealed class GetCompaniesByDataSourceStmt : QueryDbStmtBase
{
    private const string sql = "SELECT company_id, cik, data_source"
        + " FROM companies"
        + " WHERE data_source = @data_source";

    private readonly string _dataSource;
    private readonly List<Company> _companies;

    private static int _companyIdIndex = -1;
    private static int _cikIndex = -1;
    private static int _dataSourceIndex = -1;

    public GetCompaniesByDataSourceStmt(string dataSource)
        : base(sql, nameof(GetCompaniesByDataSourceStmt))
    {
        _dataSource = dataSource;
        _companies = [];
    }

    public IReadOnlyCollection<Company> Companies => _companies;

    protected override void BeforeRowProcessing(NpgsqlDataReader reader)
    {
        base.BeforeRowProcessing(reader);

        if (_companyIdIndex != -1) return;

        _companyIdIndex = reader.GetOrdinal("company_id");
        _cikIndex = reader.GetOrdinal("cik");
        _dataSourceIndex = reader.GetOrdinal("data_source");
    }

    protected override void ClearResults() => _companies.Clear();

    protected override IReadOnlyCollection<NpgsqlParameter> GetBoundParameters() =>
        [ new NpgsqlParameter<string>("data_source", _dataSource) ];

    protected override bool ProcessCurrentRow(NpgsqlDataReader reader)
    {
        var company = new Company(
            (ulong)reader.GetInt64(_companyIdIndex),
            (ulong)reader.GetInt64(_cikIndex),
            reader.GetString(_dataSourceIndex));
        _companies.Add(company);
        return true;
    }
}
