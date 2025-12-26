using System;

namespace Stocks.EDGARScraper.Models.Statements;

public record PrintStatementArgs(
    string? Cik,
    string? Concept,
    DateOnly Date,
    int MaxDepth,
    string Format,
    string? RoleName,
    bool ListStatements,
    bool ShowUsage
);
