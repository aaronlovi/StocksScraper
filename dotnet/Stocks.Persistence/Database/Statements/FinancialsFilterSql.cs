using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using Stocks.DataModels.Scoring;

namespace Stocks.Persistence.Database.Statements;

/// <summary>
/// SQL for the financials filter on a query that joins <c>companies c</c>. The SIC range
/// comes from <see cref="IndustryClassification"/> as parameters, never as literals.
/// </summary>
internal static class FinancialsFilterSql {
    internal const string CompaniesJoin = "LEFT JOIN companies c ON c.company_id = s.company_id";

    /// <summary>The WHERE predicate for a filter, or null for <see cref="FinancialsFilter.All"/>.</summary>
    internal static string? Predicate(FinancialsFilter filter) => filter switch {
        FinancialsFilter.Only => "c.sic_code BETWEEN @fin_min AND @fin_max",
        FinancialsFilter.Exclude => "(c.sic_code IS NULL OR c.sic_code NOT BETWEEN @fin_min AND @fin_max)",
        _ => null,
    };

    internal static void AddParameters(List<NpgsqlParameter> parameters, FinancialsFilter filter) {
        if (filter == FinancialsFilter.All)
            return;
        parameters.Add(new NpgsqlParameter<int>("fin_min", IndustryClassification.FinancialSicMin) { NpgsqlDbType = NpgsqlDbType.Integer });
        parameters.Add(new NpgsqlParameter<int>("fin_max", IndustryClassification.FinancialSicMax) { NpgsqlDbType = NpgsqlDbType.Integer });
    }
}
