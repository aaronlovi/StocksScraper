using MongoDB.Driver;
using System;

class Program
{
    static void Main(string[] args)
    {
        // Connect to MongoDB
        var client = new MongoClient("mongodb://root:example@localhost:27017");
        var database = client.GetDatabase("EDGAR");

        // Test: List collections in the database
        var collections = database.ListCollectionNames().ToList();
        Console.WriteLine("Collections in the EDGAR database:");
        collections.ForEach(Console.WriteLine);
    }
}
