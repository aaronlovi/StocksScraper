using Utilities;

namespace Stocks.Persistence;

public record DbStmtResult : Results
{
    private DbStmtResult(bool success, string errMsg, int numRows) : base(success, errMsg)
    {
        NumRows = numRows;
    }

    public int NumRows { get; init; }

    public static DbStmtResult StatementSuccess(int numRows) => new(true, string.Empty, numRows);

    public static DbStmtResult StatementFailure(string errMsg) => new(false, errMsg, 0);
}
