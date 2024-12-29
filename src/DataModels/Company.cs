using System.Collections.Generic;

namespace Stocks.DataModels;

public record Company(
    ulong CompanyId,
    ulong Cik,
    string DataSource,
    List<Instrument>? Instruments = null);
