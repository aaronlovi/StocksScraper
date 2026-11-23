namespace Stocks.DataModels;

/// <summary>
/// A company's SEC Standard Industrial Classification, from the EDGAR submissions file
/// (<c>sic</c>, <c>sicDescription</c>). Null code means the SEC lists none.
/// </summary>
public record CompanySic(ulong CompanyId, int? SicCode, string? SicDescription);
