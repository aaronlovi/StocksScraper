namespace Stocks.DataModels;

public record DataPointUnit(ulong UnitId, string UnitName) {
    public string UnitNameNormalized => UnitName.ToLowerInvariant();
}
