using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.Persistence.Database;

public record DbStmtResult : Result {
    private DbStmtResult(int numRows, ErrorCodes errorCode, string errMsg = "")
        : base(errorCode, errMsg, null) {
        NumRows = numRows;
    }

    public int NumRows { get; init; }

    public static DbStmtResult StatementSuccess(int numRows) => new(numRows, ErrorCodes.None, string.Empty);
    public static DbStmtResult StatementFailure(ErrorCodes errorCode, string errMsg) => new(0, errorCode, errMsg);
}
