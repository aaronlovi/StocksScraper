using System.Collections.Generic;

namespace DataModels;

public record Company(string? Cik, string Name, List<Instrument>? Instruments = null);
