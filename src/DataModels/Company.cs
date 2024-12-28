using System.Collections.Generic;

namespace DataModels;

public record Company(
    ulong CompanyId,
    ulong Cik,
    string DataSource,
    List<Instrument>? Instruments = null);
