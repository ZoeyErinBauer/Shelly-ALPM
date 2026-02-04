using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LiteDB;
using Shelly_UI.Models;

namespace Shelly_UI.Services.LocalDatabase;

public class Database
{
    private static readonly string DbFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Shelly/Shelly.db");

    private const int PageSize = 20;

    public async Task<bool> AddToDatabase(List<FlatpakModel> models)
    {
      
        try
        {
            using var db = new LiteDatabase(DbFolder);
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
    
    public async Task<bool> AddToDatabasePackages(List<PackageModel> packages)
    {
      
        try
        {
            using var db = new LiteDatabase(DbFolder);
            var col = db.GetCollection<PackageModel>("packages");
            foreach (var model in packages)
            {
                col.Upsert(model);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("error" + e);
        }

        //_ = Task.Run(EnsureIndex);
        return true;
    }

    public List<PackageModel> GetPackages()
    {
        using var db = new LiteDatabase(DbFolder);
        var col = db.GetCollection<PackageModel>("packages");
        
        var packages = col.FindAll();
        return packages.ToList();
    }

    
    public static List<T> GetNextPage<T, TKey>(
        string collection, 
        int pageNumber,
        int pageSize,
        Expression<Func<T, TKey>> orderBySelector, 
        Expression<Func<T, bool>>? predicate = null)
    {
        using var db = new LiteDatabase(DbFolder);
        var col = db.GetCollection<T>(collection);

        var query = col.Query();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return query
            .OrderBy(orderBySelector)
            .Skip(pageNumber * pageSize)
            .Limit(pageSize)
            .ToList();
    }

    private static Task EnsureIndex()
    {
        using (var db = new LiteDatabase(DbFolder))
        {
            var col = db.GetCollection<FlatpakModel>("flatpaks");
            col.EnsureIndex(x => x.Name);
            col.EnsureIndex(x => x.Categories);
        }
        return Task.CompletedTask;
    }

    public bool CollectionExists(string collectionName)
    {
        using var db = new LiteDatabase(DbFolder);
        var col = db.GetCollection<FlatpakModel>(collectionName);
        return col.Count() > 0;
    }
}