using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LiteDB;
using Shelly_UI.Models;

namespace Shelly_UI.Services.LocalDatabase;

public class Database
{
    private static readonly string dbFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Shelly/Shelly.db");

    private const int PageSize = 20;

    public async Task<bool> AddToDatabase(List<FlatpakModel> models)
    {
      
        try
        {
            using var db = new LiteDatabase(dbFolder);
            var col = db.GetCollection<FlatpakModel>("flatpaks");
            foreach (var model in models)
            {
                col.Upsert(model);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("error" + e);
        }

        _ = Task.Run(EnsureIndex);
        return true;
    }

    public async Task<List<FlatpakModel>> SearchDataBaseName(string search)
    {
        using var db = new LiteDatabase(dbFolder);
        var col = db.GetCollection<FlatpakModel>("flatpaks");

        var results = col.Query()
            .Where(x => x.Name.Contains(search))
            .OrderBy(x => x.Name)
            .Select(x => new FlatpakModel() { Id = x.Id, Name = x.Name })
            .ToList();

        return results;
    }

    public List<FlatpakModel> GetNextPage(int pageNumber, string? searchTerm = null)
    {
        using (var db = new LiteDatabase(dbFolder))
        {
            var col = db.GetCollection<FlatpakModel>("flatpaks");

            var query = col.Query();

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(x => x.Name.Contains(searchTerm) ||
                                         x.Summary.Contains(searchTerm));
            }

            return query
                .OrderBy(x => x.Name)
                .Skip(pageNumber * PageSize)
                .Limit(PageSize)
                .ToList();
        }
    }

    public Task EnsureIndex()
    {
        using (var db = new LiteDatabase(dbFolder))
        {
            var col = db.GetCollection<FlatpakModel>("flatpaks");
            col.EnsureIndex(x => x.Name);
        }
        return Task.CompletedTask;
    }

    public bool CollectionExists(string collectionName)
    {
        using var db = new LiteDatabase(dbFolder);
        var col = db.GetCollection<FlatpakModel>("flatpaks");
        return col.Count() > 0;
    }
}