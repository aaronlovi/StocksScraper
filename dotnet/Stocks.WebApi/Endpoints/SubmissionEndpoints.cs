using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.Persistence.Database;
using Stocks.Shared;
using Stocks.WebApi.Middleware;

namespace Stocks.WebApi.Endpoints;

public static class SubmissionEndpoints {
    public static void MapSubmissionEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapGet("/api/companies/{cik}/submissions", async (string cik, IDbmService dbm, CancellationToken ct) => {
            Result<Company> companyResult = await dbm.GetCompanyByCik(cik, ct);
            if (companyResult.IsFailure)
                return companyResult.ToHttpResult();

            Result<IReadOnlyCollection<Submission>> subsResult =
                await dbm.GetSubmissionsByCompanyId(companyResult.Value!.CompanyId, ct);
            if (subsResult.IsFailure)
                return subsResult.ToHttpResult();

            var items = new List<object>();
            foreach (Submission s in subsResult.Value!) {
                items.Add(new {
                    s.SubmissionId,
                    FilingType = s.FilingType.ToDisplayName(),
                    FilingCategory = s.FilingCategory.ToString(),
                    ReportDate = s.ReportDate.ToString("yyyy-MM-dd"),
                    s.FilingReference
                });
            }

            return Results.Ok(new { Items = items });
        });
    }
}
