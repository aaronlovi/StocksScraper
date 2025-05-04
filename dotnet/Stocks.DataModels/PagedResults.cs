using System.Collections.Generic;

namespace Stocks.DataModels;

public record PagedResults<T>(IReadOnlyCollection<T> Items, PaginationResponse Pagination) {
    public int NumItems => Items.Count;
}

public record PagedCompanies : PagedResults<Company> {
    public PagedCompanies(IReadOnlyCollection<Company> items, PaginationResponse pagination)
        : base(items, pagination) {
    }

    public IReadOnlyCollection<Company> Companies => Items;
}
