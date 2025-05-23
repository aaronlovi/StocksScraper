﻿namespace Stocks.DataModels.Enums;

public enum FilingType {
    Invalid = 0,
    TenK = 1,       // Edgar 10-K
    TenQ = 2,       // Edgar 10-Q
    EightK = 3,     // Edgar 8-K (Current report/interim update)
    EightK_A = 4,   // Edgar 8-K/A (Current report amendment)
    TenK_A = 5,     // Edgar 10-K/A
    TenQ_A = 6,     // Edgar 10-Q/A
    TenKT_A = 7,    // Edgar 10-KT/A (Amended Transitional Annual Report)
    TenQT_A = 8,    // Edgar 10-QT/A (Amended Transitional Quarterly Report)
    TenKT = 9,      // Edgar 10-KT (Transitional Annual Report)
    TenQT = 10,     // Edgar 10-QT (Transitional Quarterly Report)
    FortyF = 11,    // Edgar 40-F (Canadian company annual filing)
    FortyF_A = 12,  // Edgar 40-F/A (Canadian company annual filing amendment)
    TwentyF = 13,   // Edgar 20-F (Foreign company annual filing)
    TwentyF_A = 14, // Edgar 20-F/A (Foreign company annual filing amendment)
    SixK = 15,      // Edgar 6-K (Foreign company report)
    SixK_A = 16,    // Edgar 6-K/A (Foreign company report amendment)
}
