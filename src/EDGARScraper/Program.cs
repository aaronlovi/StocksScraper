using MongoDB.Bson;
using MongoDB.Driver;
using System;

class Program
{
    static void Main(string[] args)
    {
        // Connect to MongoDB
        var client = new MongoClient("mongodb://root:example@localhost:27017");
        var database = client.GetDatabase("EDGAR");

        // Create or access a collection
        var collection = database.GetCollection<BsonDocument>("TestCollection");

        // Insert a sample document
        var document = new BsonDocument
        {
            { "name", "Sample Company" },
            { "cik", "000000000" },
            { "createdAt", DateTime.UtcNow }
        };
        collection.InsertOne(document);

        Console.WriteLine("Inserted a test document into 'TestCollection'.");
    }
}
