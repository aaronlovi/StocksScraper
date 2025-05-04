using Stocks.Protocols;

namespace Stocks.Shared.ProtosExtensions;

public static class ProtosUtils {
    public static PaginationResponse CreateEmptyPaginationResponse() =>
        new() { CurrentPage = 0, TotalItems = 0, TotalPages = 0 };
}
