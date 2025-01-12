using System;

namespace Stocks.DataModels;

public record PaginationRequest
{
    public const uint DefaultMaxPageSize = 100;

    public PaginationRequest(uint pageNumber, uint pageSize, uint maxPageSize = DefaultMaxPageSize)
    {
        if (pageNumber == 0)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than 0.");
        if (pageSize == 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than 0.");
        if (pageSize > maxPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"Page size must be between 1 and {maxPageSize}.");
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    /// <summary>
    /// Page number for pagination
    /// </summary>
    public uint PageNumber { get; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public uint PageSize { get; }
}

public record PaginationResponse(
    uint CurrentPage,   // Current page number
    uint TotalItems,    // Total number of items available
    uint TotalPages)    // Total number of pages
{
    public static readonly PaginationResponse Empty = new(0, 0, 0);
}
