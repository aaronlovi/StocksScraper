namespace Stocks.DataModels.Enums;

public enum FilingType
{
    Invalid = 0,
    TenK = 1,       // Edgar 10-K
    TenQ = 2,       // Edgar 10-Q
    EightK = 3,     // Edgar 8-K
    TenK_A = 4,     // Edgar 10-K/A
    TenQ_A = 5,     // Edgar 10-Q/A
}
