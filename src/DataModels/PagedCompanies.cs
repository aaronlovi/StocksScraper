using System.Collections.Generic;

namespace Stocks.DataModels;

public record PagedCompanies(IReadOnlyCollection<Company> Companies, PaginationResponse Pagination)
{
    public int NumItems => Companies.Count;
}
