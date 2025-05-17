using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.Persistence.DistributedCaching;

public enum CacheStmtFailureReason { None, Duplicate, Other }

public record CacheStmtResult : Result {
    private CacheStmtResult(ErrorCodes errorCode, string errMsg, long numRows, CacheStmtFailureReason failureReason)
        : base(errorCode, errMsg) {
        NumRows = numRows;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Could represent a number of rows found, a database id, or something else depending on the context.
    /// </summary>
    public long NumRows { get; init; }

    public CacheStmtFailureReason FailureReason { get; init; }

    public static new CacheStmtResult Success(long numRows) => new(ErrorCodes.None, string.Empty, numRows, CacheStmtFailureReason.None);
    public static CacheStmtResult Failure(ErrorCodes errorCode, string errMsg, CacheStmtFailureReason failureReason = CacheStmtFailureReason.Other)
        => new(errorCode, errMsg, 0, failureReason);
}
