using System.Collections.Generic;

namespace DataModels;

public record Company(
    string? Cik,
    string Name,
    string DataSource,
    List<Instrument>? Instruments = null);
