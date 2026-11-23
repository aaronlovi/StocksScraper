using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using EDGARScraper.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.EDGARScraper.Tests;

public sealed class CompanySicImporterTests : IDisposable {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;
    private readonly string _zipPath = Path.Combine(Path.GetTempPath(), $"sic-test-{Guid.NewGuid():N}.zip");

    public void Dispose() {
        if (File.Exists(_zipPath))
            File.Delete(_zipPath);
    }

    private void WriteZip(Dictionary<string, string> files) {
        using ZipArchive archive = ZipFile.Open(_zipPath, ZipArchiveMode.Create);
        foreach ((string name, string content) in files) {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
    }

    private static string CompanyFile(string cik, string sic, string description) =>
        "{\"cik\":\"" + cik + "\",\"entityType\":\"operating\",\"sic\":\"" + sic
        + "\",\"sicDescription\":\"" + description
        + "\",\"ownerOrg\":\"02 Finance\",\"name\":\"X\",\"filings\":{\"recent\":{\"accessionNumber\":[]},\"files\":[]}}";

    private async Task<Dictionary<ulong, CompanySic>> ReadSics() {
        Result<IReadOnlyCollection<CompanySic>> read = await _dbm.GetCompanySics(_ct);
        var byId = new Dictionary<ulong, CompanySic>();
        foreach (CompanySic sic in read.Value!)
            byId[sic.CompanyId] = sic;
        return byId;
    }

    [Fact]
    public async Task ImportAsync_StoresSicForKnownCompanies() {
        _ = await _dbm.BulkInsertCompanies([
            new Company(1, 320193, "EDGAR"),
            new Company(2, 70858, "EDGAR"),
            new Company(3, 12345, "EDGAR"),
        ], _ct);
        WriteZip(new Dictionary<string, string> {
            ["CIK0000320193.json"] = CompanyFile("0000320193", "3571", "Electronic Computers"),
            ["CIK0000070858.json"] = CompanyFile("0000070858", "6021", "National Commercial Banks"),
            ["CIK0000012345.json"] = CompanyFile("0000012345", "", ""),
            ["CIK0000070858-submissions-001.json"] = """{"accessionNumber":["x"],"filingDate":["2001-01-01"]}""",
            ["CIK0000099999.json"] = CompanyFile("0000099999", "6311", "Life Insurance"),
        });
        var importer = new CompanySicImporter(_dbm, NullLogger<CompanySicImporter>.Instance);

        Result<CompanySicImportStats> result = await importer.ImportAsync(_zipPath, 2, _ct);

        Assert.True(result.IsSuccess);
        CompanySicImportStats stats = result.Value!;
        Assert.Equal(4, stats.CompanyFiles);
        Assert.Equal(3, stats.WithSic);
        Assert.Equal(1, stats.UnmatchedCiks);
        Assert.Equal(3, stats.Updated);
        Assert.Equal(0, stats.Failed);

        Dictionary<ulong, CompanySic> sics = await ReadSics();
        Assert.Equal(2, sics.Count);
        Assert.Equal(3571, sics[1].SicCode);
        Assert.Equal("Electronic Computers", sics[1].SicDescription);
        Assert.Equal(6021, sics[2].SicCode);
    }

    [Fact]
    public async Task ImportAsync_EmptySicClearsAnEarlierCode() {
        _ = await _dbm.BulkInsertCompanies([new Company(1, 320193, "EDGAR")], _ct);
        _ = await _dbm.UpdateCompanySic([new CompanySic(1, 6021, "National Commercial Banks")], _ct);
        WriteZip(new Dictionary<string, string> {
            ["CIK0000320193.json"] = CompanyFile("0000320193", "", ""),
        });
        var importer = new CompanySicImporter(_dbm, NullLogger<CompanySicImporter>.Instance);

        Result<CompanySicImportStats> result = await importer.ImportAsync(_zipPath, 100, _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(await ReadSics());
    }

    [Fact]
    public async Task ImportAsync_MalformedFileIsCountedAndSkipped() {
        _ = await _dbm.BulkInsertCompanies([new Company(1, 320193, "EDGAR")], _ct);
        WriteZip(new Dictionary<string, string> {
            ["CIK0000000001.json"] = "{ not json",
            ["CIK0000320193.json"] = CompanyFile("0000320193", "3571", "Electronic Computers"),
        });
        var importer = new CompanySicImporter(_dbm, NullLogger<CompanySicImporter>.Instance);

        Result<CompanySicImportStats> result = await importer.ImportAsync(_zipPath, 100, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Failed);
        Assert.Equal(3571, (await ReadSics())[1].SicCode);
    }

    [Fact]
    public async Task ImportAsync_MissingArchiveFails() {
        var importer = new CompanySicImporter(_dbm, NullLogger<CompanySicImporter>.Instance);

        Result<CompanySicImportStats> result = await importer.ImportAsync(_zipPath, 100, _ct);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }
}
