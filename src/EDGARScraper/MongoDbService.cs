using DataModels;
using DataModels.XbrlFileModels;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Utilities;

namespace EDGARScraper;

internal class MongoDbService
{
    private static readonly ReplaceOptions UpsertOptions = new() { IsUpsert = true };
    private const int ConnectionCount = 100;
    private const string ConnectionString = "mongodb://root:example@localhost:27017";

    private readonly SemaphoreSlim _semaphore;
    private IMongoDatabase? _database = null;

    internal MongoDbService()
    {
        _semaphore = new(ConnectionCount);
        _database = null;
    }

    internal IMongoCollection<BsonDocument> GetCollection(string collectionName)
    {
        IMongoDatabase database = GetDatabase();
        return database.GetCollection<BsonDocument>(collectionName);
    }

    internal async Task CreateIndices()
    {
        IMongoCollection<BsonDocument> collection = GetCollection("Companies");
        IndexKeysDefinition<BsonDocument> indexKeys = Builders<BsonDocument>.IndexKeys
            .Ascending("name")
            .Ascending("cik");
        var indexModel = new CreateIndexModel<BsonDocument>(indexKeys);
        await collection.Indexes.CreateOneAsync(indexModel);

        collection = GetCollection("RawFinancialData");
        indexKeys = Builders<BsonDocument>.IndexKeys.Ascending("cik");
        indexModel = new CreateIndexModel<BsonDocument>(indexKeys);
        await collection.Indexes.CreateOneAsync(indexModel);

        Console.WriteLine("Indices created successfully.");
    }

    internal async Task SaveRawData(string cik, string url, string rawData)
    {
        var collection = GetCollection("RawFilings");
        var document = new BsonDocument
        {
            { "company", new BsonDocument { { "cik", cik }, { "name", "Apple Inc." } } },
            { "url", url },
            { "raw_data", rawData },
            { "fetched_at", DateTime.UtcNow }
        };
        ReplaceOneResult result = await collection.ReplaceOneAsync(
            filter: Builders<BsonDocument>.Filter.Eq("company.cik", cik),
            replacement: document,
            options: new ReplaceOptions { IsUpsert = true }
        );
        Console.WriteLine("Fetched and saved rendered HTML successfully.");
        Console.WriteLine("Results: Matched={0}, Modified={1}, Upserted={2}",
            result.MatchedCount, result.ModifiedCount, result.UpsertedId != null);
    }

    /// <summary>
    /// Raw filings represents the landing page where the list of filings are displayed.
    /// It is a collection of 10-Ks.
    /// </summary>
    internal async Task<RawFilingData> GetRawFilingsRawData(string? cik = null)
    {
        IMongoCollection<BsonDocument> collection = GetCollection("RawFilings");

        FilterDefinition<BsonDocument> filter = string.IsNullOrEmpty(cik)
            ? Builders<BsonDocument>.Filter.Empty
            : Builders<BsonDocument>.Filter.Eq("company.cik", cik);

        BsonDocument? document = await collection.Find(filter).FirstOrDefaultAsync();
        if (document is null)
        {
            Console.WriteLine("No raw filing found for CIK {0}.", cik ?? "[NULL]");
            return RawFilingData.Empty;
        }

        return new(document["raw_data"].AsString, document["company"].AsBsonDocument);
    }

    internal async Task SaveParsedFilings(RawFilingData rawFilingData, BsonArray filings)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("company.cik", rawFilingData.Cik);
        var collection = GetCollection("ParsedFilings");
        var document = new BsonDocument
        {
            { "company", rawFilingData.CompanyBson },
            { "filings", filings },
            { "parsed_at", DateTime.UtcNow }
        };
        ReplaceOneResult result = await collection.ReplaceOneAsync(filter, document, UpsertOptions);

        Console.WriteLine("Parsed and saved filing metadata. Results: Matched={0}, Modified={1}, Upserted={2}",
            result.MatchedCount, result.ModifiedCount, result.UpsertedId != null);
    }

    internal async Task<ParsedFilingData> GetParsedFilings(string? cik = null)
    {
        IMongoCollection<BsonDocument> collection = GetCollection("ParsedFilings");

        FilterDefinition<BsonDocument> filter = string.IsNullOrEmpty(cik)
            ? Builders<BsonDocument>.Filter.Empty
            : Builders<BsonDocument>.Filter.Eq("company.cik", cik);

        BsonDocument? document = await collection.Find(new BsonDocument()).FirstOrDefaultAsync();
        if (document is null)
        {
            Console.WriteLine("No parsed filings found in the database.");
            return ParsedFilingData.Empty;
        }

        return new(document["company"].AsBsonDocument, document["filings"].AsBsonArray);
    }

    internal async Task SaveFilingDetailDocuments(string cik, string filingDate, string filingType, string documentLink, BsonDocument document)
    {
        IMongoCollection<BsonDocument> collection = GetCollection("FilingDetailDocuments");

        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("company.cik", cik),
            Builders<BsonDocument>.Filter.Eq("filing_date", filingDate),
            Builders<BsonDocument>.Filter.Eq("filing_type", filingType));
        ReplaceOneResult result = await collection.ReplaceOneAsync(filter, document, UpsertOptions);

        Console.WriteLine("Saved Filing documents: {0}. Results: Matched={1}, Modified={2}, Upserted={3}",
            documentLink, result.MatchedCount, result.ModifiedCount, result.UpsertedId is not null);
    }

    internal async Task<List<FilingDetails>> GetFilingDetailDocuments()
    {
        IMongoCollection<BsonDocument> collection = GetCollection("FilingDetailDocuments");

        var filingDetailDocuments = new List<FilingDetails>();
        List<BsonDocument> documents = await collection.Find(new BsonDocument()).ToListAsync();
        foreach (var doc in documents)
        {
            FilingDetails filingDetails = FilingDetails.FromBson(doc);
            filingDetailDocuments.Add(filingDetails);
        }

        return filingDetailDocuments;
    }

    internal async Task SaveXBRLLinks(string cik, string filingDate, BsonDocument document)
    {
        IMongoCollection<BsonDocument> collection = GetCollection("XBRLLinks");
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("company.cik", cik),
            Builders<BsonDocument>.Filter.Eq("filing_date", filingDate));
        ReplaceOneResult result = await collection.ReplaceOneAsync(filter, document, UpsertOptions);

        Console.WriteLine("Saved XBRL links for {0}. Results: Matched={1}, Modified={2}, Upserted={3}",
            cik, result.MatchedCount, result.ModifiedCount, result.UpsertedId is not null);
    }

    internal async Task<List<CompanyXbrlLink>> GetXBRLLinks()
    {
        IMongoCollection<BsonDocument> collection = GetCollection("XBRLLinks");
        var xbrlLinks = new List<CompanyXbrlLink>();
        List<BsonDocument> documents = await collection.Find(new BsonDocument()).ToListAsync();
        foreach (var doc in documents)
            xbrlLinks.Add(CompanyXbrlLink.FromBson(doc));
        return xbrlLinks;
    }

    internal async Task SaveXBRLDocument(CompanyXbrlLink companyXbrlLink, BsonDocument document)
    {
        IMongoCollection<BsonDocument> collection = GetCollection("XBRLDocuments");

        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("company.cik", companyXbrlLink.Cik),
            Builders<BsonDocument>.Filter.Eq("filing_date", companyXbrlLink.FilingDate),
            Builders<BsonDocument>.Filter.Eq("xbrl_url", companyXbrlLink.XbrlUrl));
        ReplaceOneResult result = await collection.ReplaceOneAsync(filter, document, UpsertOptions);

        Console.WriteLine("Saved XBRL document for {0}. Results: Matched={1}, Modified={2}, Upserted={3}",
            companyXbrlLink.Cik, result.MatchedCount, result.ModifiedCount, result.UpsertedId is not null);
    }

    internal async Task<BsonDocument?> GetOneXbrlDocument()
    {
        IMongoCollection<BsonDocument> collection = GetCollection("XBRLDocuments");
        BsonDocument? xbrlDoc = await collection.Find(new BsonDocument()).FirstOrDefaultAsync();
        if (xbrlDoc is null)
        {
            Console.WriteLine("No XBRL documents found in the database.");
            return null;
        }
        return xbrlDoc;
    }

    internal async Task SaveFinancialData(string cik, string filingDate, BsonDocument financialData)
    {
        IMongoCollection<BsonDocument> collection = GetCollection("FinancialData");
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("company.cik", cik),
            Builders<BsonDocument>.Filter.Eq("filing_date", filingDate));
        ReplaceOneResult result = await collection.ReplaceOneAsync(filter, financialData, UpsertOptions);

        Console.WriteLine("Saved financial data for {0}. Results: Matched={1}, Modified={2}, Upserted={3}",
            cik, result.MatchedCount, result.ModifiedCount, result.UpsertedId is not null);
    }

    internal async Task<Results> SaveXbrlFileDataBatch(List<(XbrlJson, IReadOnlyDictionary<string, Dictionary<string, Dictionary<DatePair, DataPoint>>>)> batch)
    {
        using var _ = await _semaphore.AcquireAsync();

        try
        {
            IMongoCollection<BsonDocument> collection = GetCollection("RawFinancialData");

            var bulkOperations = new List<WriteModel<BsonDocument>>();

            foreach ((XbrlJson xbrlJson, IReadOnlyDictionary<string, Dictionary<string, Dictionary<DatePair, DataPoint>>> dataPoints) in batch)
            {
                BsonDocument document = BsonDocumentExtensions.XBRLFileDataToBsonDocument(dataPoints);
                document.Add("cik", xbrlJson.CikString);

                FilterDefinitionBuilder<BsonDocument> filterBuilder = Builders<BsonDocument>.Filter;
                FilterDefinition<BsonDocument> filter = filterBuilder.Eq("cik", xbrlJson.CikString);
                
                var upsertOne = new ReplaceOneModel<BsonDocument>(filter, document) { IsUpsert = true };
                bulkOperations.Add(upsertOne);
            }

            if (bulkOperations.Count > 0)
                await collection.BulkWriteAsync(bulkOperations);

            return Results.Success;
        }
        catch (Exception ex)
        {
            return Results.FailureResult("Error in SaveCompaniesBulk - " + ex.Message);
        }
    }

    #region PRIVATE HELPER METHODS

    private IMongoDatabase GetDatabase()
    {
        if (_database is null)
        {
            var settings = MongoClientSettings.FromConnectionString(ConnectionString);
            settings.MaxConnectionPoolSize = ConnectionCount; // Adjust as needed
            var client = new MongoClient(settings);
            _database = client.GetDatabase(Constants.EdgarDataSource);
        }
        return _database;
    }

    #endregion
}
