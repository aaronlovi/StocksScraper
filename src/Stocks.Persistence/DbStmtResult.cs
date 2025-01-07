using Stocks.Shared;

namespace Stocks.Persistence;

public enum DbStmtFailureReason { Invalid, Duplicate, Other }

public record DbStmtResult : Results
{
    private DbStmtResult(bool success, string errMsg, int numRows, DbStmtFailureReason failureReason)
        : base(success, errMsg)
    {
        NumRows = numRows;
        FailureReason = failureReason;
    }

    public int NumRows { get; init; }
    public DbStmtFailureReason FailureReason { get; init; }

    public static DbStmtResult StatementSuccess(int numRows) => new(true, string.Empty, numRows, DbStmtFailureReason.Invalid);

    public static DbStmtResult StatementFailure(string errMsg, DbStmtFailureReason failureReason = DbStmtFailureReason.Other)
        => new(false, errMsg, 0, failureReason);
}
