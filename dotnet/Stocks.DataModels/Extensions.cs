namespace Stocks.DataModels;

public static class Extensions {
    public static Protocols.PaginationResponse ToProtosPaginationResponse(this PaginationResponse pr) =>
        new() { CurrentPage = pr.CurrentPage, TotalItems = pr.TotalItems, TotalPages = pr.TotalPages };
}
